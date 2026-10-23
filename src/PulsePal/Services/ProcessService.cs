using PulsePal.Models;
using System.Diagnostics;

namespace PulsePal.Services;

/// <summary>
/// Samples processes and computes CPU from total processor-time deltas.
/// This deliberately does not use a per-process performance counter.
/// </summary>
public sealed class ProcessService : IProcessService
{
    private static readonly HashSet<string> ProtectedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "smss", "csrss", "wininit", "winlogon",
        "services", "lsass", "svchost", "dwm", "fontdrvhost", "sihost",
        "Memory Compression", "MsMpEng"
    };

    private readonly int _logicalCores = Environment.ProcessorCount;
    private readonly Dictionary<int, TimeSpan> _lastProcessorTimes = [];
    private readonly Dictionary<int, PathCacheEntry> _pathCache = [];
    private DateTime _lastSampleAt;

    public IReadOnlyList<ProcessSample> Sample()
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = _lastSampleAt == default ? 0 : (now - _lastSampleAt).TotalSeconds;
        _lastSampleAt = now;

        var samples = new List<ProcessSample>(256);
        var seenPids = new HashSet<int>();
        var currentPid = Environment.ProcessId;

        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            return samples;
        }

        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    var pid = process.Id;
                    if (pid == 0)
                    {
                        continue;
                    }

                    seenPids.Add(pid);
                    var name = process.ProcessName;
                    var startedAt = ReadStartTime(process, now);
                    var path = ReadPath(process, pid, startedAt);
                    var memoryMb = SafeWorkingSetMb(process);
                    var totalProcessorTime = SafeProcessorTime(process);

                    double cpuPercent = 0;
                    if (totalProcessorTime is not null &&
                        _lastProcessorTimes.TryGetValue(pid, out var previous) &&
                        elapsedSeconds > 0.05)
                    {
                        var delta = totalProcessorTime.Value - previous;
                        if (delta > TimeSpan.Zero)
                        {
                            // Per-process CPU is a share of all logical cores, matching
                            // Task Manager's 0-100% scale across the whole machine.
                            cpuPercent = delta.TotalSeconds / elapsedSeconds / _logicalCores * 100;
                        }
                    }

                    if (totalProcessorTime is not null)
                    {
                        _lastProcessorTimes[pid] = totalProcessorTime.Value;
                    }

                    var isProtected = ProtectedProcessNames.Contains(name) ||
                                      pid == currentPid ||
                                      pid <= 4;

                    samples.Add(new ProcessSample(
                        pid,
                        name,
                        Math.Clamp(cpuPercent, 0, 100),
                        memoryMb,
                        path,
                        CanEnd: !isProtected,
                        IsProtected: isProtected,
                        startedAt));
                }
                catch
                {
                    // Process vanished or access was denied. Skip just this row.
                }
            }
        }

        foreach (var stale in _lastProcessorTimes.Keys.Where(pid => !seenPids.Contains(pid)).ToList())
        {
            _lastProcessorTimes.Remove(stale);
        }

        foreach (var stale in _pathCache.Keys.Where(pid => !seenPids.Contains(pid)).ToList())
        {
            _pathCache.Remove(stale);
        }

        return samples
            .OrderByDescending(p => p.CpuPercent)
            .ThenByDescending(p => p.MemoryMb)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public (bool Success, string Message) EndProcess(int pid)
    {
        if (pid == Environment.ProcessId)
        {
            return (false, "PulsePal is not going to unplug itself.");
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            if (ProtectedProcessNames.Contains(process.ProcessName) || pid <= 4)
            {
                return (false, "That process is doing protected Windows work.");
            }

            process.Kill();
            process.WaitForExit(2500);
            return (true, $"{process.ProcessName} has left the room.");
        }
        catch (ArgumentException)
        {
            return (false, "That process already packed up and left.");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Windows said no. Restart PulsePal as admin and try again.");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            return (false, "Windows said no. Restart PulsePal as admin and try again.");
        }
        catch (Exception ex)
        {
            return (false, $"Could not end that process: {ex.Message}");
        }
    }

    private DateTime ReadStartTime(Process process, DateTime fallback)
    {
        try
        {
            return process.StartTime;
        }
        catch
        {
            return fallback;
        }
    }

    private string? ReadPath(Process process, int pid, DateTime startedAt)
    {
        if (_pathCache.TryGetValue(pid, out var cached) &&
            Math.Abs((cached.StartedAt - startedAt).TotalSeconds) < 1)
        {
            return cached.Path;
        }

        string? path = null;
        try
        {
            path = process.MainModule?.FileName;
        }
        catch
        {
            // Protected processes frequently deny MainModule access.
        }

        _pathCache[pid] = new PathCacheEntry(startedAt, path);
        return path;
    }

    private static double SafeWorkingSetMb(Process process)
    {
        try
        {
            return Math.Max(0, process.WorkingSet64) / 1024d / 1024d;
        }
        catch
        {
            return 0;
        }
    }

    private static TimeSpan? SafeProcessorTime(Process process)
    {
        try
        {
            return process.TotalProcessorTime;
        }
        catch
        {
            return null;
        }
    }

    private sealed record PathCacheEntry(DateTime StartedAt, string? Path);
}
