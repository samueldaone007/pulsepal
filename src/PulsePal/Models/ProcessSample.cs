namespace PulsePal.Models;

/// <summary>A single process reading. CpuPercent is computed from processor-time deltas.</summary>
public sealed record ProcessSample(
    int Pid,
    string Name,
    double CpuPercent,
    double MemoryMb,
    string? Path,
    bool CanEnd,
    bool IsProtected,
    DateTime StartedAt);
