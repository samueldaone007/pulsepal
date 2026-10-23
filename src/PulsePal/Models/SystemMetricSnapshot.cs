namespace PulsePal.Models;

/// <summary>A friendly, UI-independent view of the machine at one instant.</summary>
public sealed record SystemMetricSnapshot(
    DateTime Timestamp,
    double? CpuPercent,
    double CpuGhz,
    int LogicalCores,
    string CpuName,
    double? MemoryPercent,
    double? MemoryUsedGb,
    double? MemoryTotalGb,
    double? DiskActivityPercent,
    double? DiskReadBytesPerSecond,
    double? DiskWriteBytesPerSecond,
    double? NetworkDownBytesPerSecond,
    double? NetworkUpBytesPerSecond,
    string? NetworkAdapterId,
    string? NetworkAdapterName,
    IReadOnlyList<NetworkAdapterOption> NetworkAdapters);
