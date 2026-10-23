using Microsoft.Win32;
using PulsePal.Models;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace PulsePal.Services;

/// <summary>
/// Reads the headline numbers. Performance counters are preferred where they work,
/// with native Windows fallbacks for machines where counter categories are localized or broken.
/// </summary>
public sealed class MetricsService : IMetricsService
{
    private readonly object _sync = new();
    private readonly int _logicalCores = Environment.ProcessorCount;
    private readonly string _cpuName;
    private readonly double _baseGhz;

    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _diskTimeCounter;
    private PerformanceCounter? _diskReadCounter;
    private PerformanceCounter? _diskWriteCounter;
    private bool _countersInitialized;
    private DateTime _counterWarmupStartedAt;

    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private DateTime _lastSystemTimesAt;

    private string? _selectedAdapterId;
    private string? _autoAdapterId;
    private readonly Dictionary<string, AdapterBaseline> _adapterBaselines = [];
    private bool _disposed;

    public MetricsService()
    {
        (_cpuName, _baseGhz) = ReadProcessorInfo();
    }

    public SystemMetricSnapshot Sample()
    {
        lock (_sync)
        {
            var now = DateTime.UtcNow;
            EnsureCounters();
            var cpu = ReadCpu(now);
            var memory = ReadMemory();
            var disk = ReadDisk();
            var adapters = ReadAdapters();
            var network = ReadNetwork(adapters, now);

            return new SystemMetricSnapshot(
                DateTime.Now,
                cpu,
                _baseGhz,
                _logicalCores,
                _cpuName,
                memory.Percent,
                memory.UsedGb,
                memory.TotalGb,
                disk.ActivityPercent,
                disk.ReadBytesPerSecond,
                disk.WriteBytesPerSecond,
                network.DownBytesPerSecond,
                network.UpBytesPerSecond,
                network.Adapter?.Id,
                network.Adapter?.Name,
                adapters);
        }
    }

    public IReadOnlyList<NetworkAdapterOption> GetNetworkAdapters()
    {
        lock (_sync)
        {
            return ReadAdapters();
        }
    }

    public void SelectNetworkAdapter(string? adapterId)
    {
        lock (_sync)
        {
            _selectedAdapterId = string.IsNullOrWhiteSpace(adapterId) ? null : adapterId;
            // Force fresh deltas for the newly selected adapter.
            _adapterBaselines.Clear();
        }
    }

    private void EnsureCounters()
    {
        if (_countersInitialized)
        {
            return;
        }

        _countersInitialized = true;
        _counterWarmupStartedAt = DateTime.UtcNow;

        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
            _ = _cpuCounter.NextValue();
        }
        catch
        {
            _cpuCounter?.Dispose();
            _cpuCounter = null;
        }

        // PhysicalDisk is the best activity number, but LogicalDisk is a useful fallback.
        if (!TryCreateDiskCounters("PhysicalDisk"))
        {
            TryCreateDiskCounters("LogicalDisk");
        }
    }

    private bool TryCreateDiskCounters(string category)
    {
        try
        {
            _diskTimeCounter = new PerformanceCounter(category, "% Disk Time", "_Total", readOnly: true);
            _diskReadCounter = new PerformanceCounter(category, "Disk Read Bytes/sec", "_Total", readOnly: true);
            _diskWriteCounter = new PerformanceCounter(category, "Disk Write Bytes/sec", "_Total", readOnly: true);
            _ = _diskTimeCounter.NextValue();
            _ = _diskReadCounter.NextValue();
            _ = _diskWriteCounter.NextValue();
            return true;
        }
        catch
        {
            _diskTimeCounter?.Dispose();
            _diskReadCounter?.Dispose();
            _diskWriteCounter?.Dispose();
            _diskTimeCounter = null;
            _diskReadCounter = null;
            _diskWriteCounter = null;
            return false;
        }
    }

    private double? ReadCpu(DateTime now)
    {
        // The perf counter needs a warm-up window before its first meaningful value.
        if (_cpuCounter is not null && now - _counterWarmupStartedAt > TimeSpan.FromSeconds(1))
        {
            try
            {
                var value = _cpuCounter.NextValue();
                if (!double.IsNaN(value) && !double.IsInfinity(value))
                {
                    return Math.Clamp(value, 0, 100);
                }
            }
            catch
            {
                _cpuCounter.Dispose();
                _cpuCounter = null;
            }
        }

        return ReadCpuFallback();
    }

    /// <summary>
    /// Kernel time includes idle time on Windows. Busy = total - idle, which keeps
    /// the fallback correct even when performance counters are unavailable.
    /// </summary>
    private double? ReadCpuFallback()
    {
        try
        {
            if (!GetSystemTimes(out var idleFileTime, out var kernelFileTime, out var userFileTime))
            {
                return null;
            }

            var idle = idleFileTime.ToUInt64();
            var kernel = kernelFileTime.ToUInt64();
            var user = userFileTime.ToUInt64();

            if (_lastSystemTimesAt == default)
            {
                _lastIdle = idle;
                _lastKernel = kernel;
                _lastUser = user;
                _lastSystemTimesAt = DateTime.UtcNow;
                return 0;
            }

            var idleDelta = idle - _lastIdle;
            var totalDelta = (kernel - _lastKernel) + (user - _lastUser);
            _lastIdle = idle;
            _lastKernel = kernel;
            _lastUser = user;
            _lastSystemTimesAt = DateTime.UtcNow;

            if (totalDelta == 0)
            {
                return 0;
            }

            var busy = totalDelta > idleDelta ? totalDelta - idleDelta : 0;
            return Math.Clamp(busy * 100.0 / totalDelta, 0, 100);
        }
        catch
        {
            return null;
        }
    }

    private (double? Percent, double? UsedGb, double? TotalGb) ReadMemory()
    {
        try
        {
            var status = new MemoryStatusEx
            {
                Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
            };

            if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
            {
                return (null, null, null);
            }

            var used = status.TotalPhys - status.AvailPhys;
            return (
                used * 100.0 / status.TotalPhys,
                used / 1024d / 1024d / 1024d,
                status.TotalPhys / 1024d / 1024d / 1024d);
        }
        catch
        {
            return (null, null, null);
        }
    }

    private (double? ActivityPercent, double? ReadBytesPerSecond, double? WriteBytesPerSecond) ReadDisk()
    {
        if (_diskTimeCounter is null || _diskReadCounter is null || _diskWriteCounter is null)
        {
            return (null, null, null);
        }

        try
        {
            var activity = _diskTimeCounter.NextValue();
            var read = _diskReadCounter.NextValue();
            var write = _diskWriteCounter.NextValue();

            if (double.IsNaN(activity) || double.IsInfinity(activity))
            {
                activity = 0;
            }

            return (
                Math.Clamp(activity, 0, 100),
                Math.Max(0, read),
                Math.Max(0, write));
        }
        catch
        {
            _diskTimeCounter.Dispose();
            _diskReadCounter.Dispose();
            _diskWriteCounter.Dispose();
            _diskTimeCounter = null;
            _diskReadCounter = null;
            _diskWriteCounter = null;
            return (null, null, null);
        }
    }

    private List<NetworkAdapterOption> ReadAdapters()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .Select(n =>
                {
                    long speed = 0;
                    var hasGateway = false;
                    try
                    {
                        speed = n.Speed;
                    }
                    catch
                    {
                        // Some virtual adapters throw when asked for link speed.
                    }

                    try
                    {
                        hasGateway = n.GetIPProperties().GatewayAddresses
                            .Any(g => g.Address is not null && !g.Address.Equals(System.Net.IPAddress.Any));
                    }
                    catch
                    {
                        // Adapter metadata is best-effort.
                    }

                    return new NetworkAdapterOption(n.Id, n.Name, n.Description, speed, hasGateway);
                })
                .OrderByDescending(a => a.HasGateway)
                .ThenByDescending(a => a.IsPhysical)
                .ThenByDescending(a => a.SpeedBitsPerSecond)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private (double? DownBytesPerSecond, double? UpBytesPerSecond, NetworkAdapterOption? Adapter) ReadNetwork(
        IReadOnlyList<NetworkAdapterOption> adapters,
        DateTime now)
    {
        if (adapters.Count == 0)
        {
            _adapterBaselines.Clear();
            _autoAdapterId = null;
            return (null, null, null);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var downRates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var upRates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var adapter in adapters)
        {
            seen.Add(adapter.Id);
            try
            {
                var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => string.Equals(n.Id, adapter.Id, StringComparison.OrdinalIgnoreCase));
                if (networkInterface is null)
                {
                    continue;
                }

                var stats = networkInterface.GetIPv4Statistics();

                if (_adapterBaselines.TryGetValue(adapter.Id, out var previous))
                {
                    var elapsed = (now - previous.At).TotalSeconds;
                    if (elapsed > 0.05)
                    {
                        var receivedDelta = stats.BytesReceived >= previous.Received
                            ? stats.BytesReceived - previous.Received
                            : 0;
                        var sentDelta = stats.BytesSent >= previous.Sent
                            ? stats.BytesSent - previous.Sent
                            : 0;
                        downRates[adapter.Id] = Math.Max(0, receivedDelta) / elapsed;
                        upRates[adapter.Id] = Math.Max(0, sentDelta) / elapsed;
                        rates[adapter.Id] = downRates[adapter.Id] + upRates[adapter.Id];
                    }
                }

                _adapterBaselines[adapter.Id] = new AdapterBaseline(
                    stats.BytesReceived,
                    stats.BytesSent,
                    now);
            }
            catch
            {
                // A flaky adapter should not take down the headline metrics.
            }
        }

        foreach (var stale in _adapterBaselines.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _adapterBaselines.Remove(stale);
        }

        // Optimistically follow the adapter carrying the most traffic. The user's
        // explicit choice always wins, even if another adapter is busier.
        if (rates.Count > 0)
        {
            var busiest = rates.OrderByDescending(kv => kv.Value).First();
            if (busiest.Value > 128 * 1024 || string.IsNullOrWhiteSpace(_autoAdapterId))
            {
                _autoAdapterId = busiest.Key;
            }
        }

        var effectiveId = _selectedAdapterId ?? _autoAdapterId;
        var selected = adapters.FirstOrDefault(a => string.Equals(a.Id, effectiveId, StringComparison.OrdinalIgnoreCase))
                       ?? adapters[0];

        return (
            downRates.TryGetValue(selected.Id, out var down) ? down : 0,
            upRates.TryGetValue(selected.Id, out var up) ? up : 0,
            selected);
    }

    private static (string Name, double Ghz) ReadProcessorInfo()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            var name = key?.GetValue("ProcessorNameString") as string;
            var mhz = key?.GetValue("~MHz");
            if (name is not null)
            {
                name = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }

            var ghz = mhz is int m ? m / 1000d : 0;
            return (name?.Trim() ?? "Processor", ghz > 0 ? ghz : 0);
        }
        catch
        {
            return ("Processor", 0);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cpuCounter?.Dispose();
            _diskTimeCounter?.Dispose();
            _diskReadCounter?.Dispose();
            _diskWriteCounter?.Dispose();
        }
    }

    private sealed record AdapterBaseline(long Received, long Sent, DateTime At);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
