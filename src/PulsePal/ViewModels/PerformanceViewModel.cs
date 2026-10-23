using CommunityToolkit.Mvvm.ComponentModel;
using PulsePal.Models;

namespace PulsePal.ViewModels;

/// <summary>
/// Reuses the same card series as the Overview so the Performance page has one
/// source of truth and does not sample the machine twice.
/// </summary>
public partial class PerformanceViewModel : ObservableObject
{
    public PerformanceViewModel(OverviewViewModel overview)
    {
        Cpu = overview.Cpu;
        Memory = overview.Memory;
        Disk = overview.Disk;
        Network = overview.Network;
    }

    public CpuCardViewModel Cpu { get; }

    public MemoryCardViewModel Memory { get; }

    public DiskCardViewModel Disk { get; }

    public NetworkCardViewModel Network { get; }

    [ObservableProperty]
    private string _lastUpdatedText = "waiting for first sample";

    [ObservableProperty]
    private string _machineText = "local machine";

    [ObservableProperty]
    private string _cpuMetaText = "logical cores";

    [ObservableProperty]
    private string _memoryMetaText = "memory total n/a";

    [ObservableProperty]
    private string _networkMetaText = "no active adapter";

    [ObservableProperty]
    private string _windowText = "0 / 60 samples";

    public string WindowDescription => "one-minute ring / one tick per second";

    public void ApplyMetrics(SystemMetricSnapshot snapshot)
    {
        LastUpdatedText = $"last tick {snapshot.Timestamp:HH:mm:ss}";
        MachineText = string.IsNullOrWhiteSpace(snapshot.CpuName) ? "local machine" : snapshot.CpuName;
        CpuMetaText = snapshot.CpuGhz > 0
            ? $"{snapshot.LogicalCores} logical cores  /  {snapshot.CpuGhz:0.0} GHz base"
            : $"{snapshot.LogicalCores} logical cores";
        MemoryMetaText = snapshot.MemoryTotalGb is double total
            ? $"{total:0.0} GB installed"
            : "memory total n/a";
        NetworkMetaText = snapshot.NetworkAdapterName ?? "no active adapter";
        WindowText = $"{Cpu.History.Count} / 60 samples";
    }
}
