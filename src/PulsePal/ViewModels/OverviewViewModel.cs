using CommunityToolkit.Mvvm.ComponentModel;
using PulsePal.Models;
using PulsePal.Services;
using System.Collections.ObjectModel;

namespace PulsePal.ViewModels;

public partial class OverviewViewModel : ObservableObject
{
    public OverviewViewModel(IMetricsService metricsService)
    {
        Cpu = new CpuCardViewModel();
        Memory = new MemoryCardViewModel();
        Disk = new DiskCardViewModel();
        Network = new NetworkCardViewModel(metricsService);
    }

    public CpuCardViewModel Cpu { get; }

    public MemoryCardViewModel Memory { get; }

    public DiskCardViewModel Disk { get; }

    public NetworkCardViewModel Network { get; }

    public ObservableCollection<ProcessSample> TopProcesses { get; } = [];

    public ObservableCollection<DriveSnapshot> Drives { get; } = [];

    [ObservableProperty]
    private bool _hasProcesses;

    [ObservableProperty]
    private bool _hasDrives;

    [ObservableProperty]
    private string _moreDrivesText = string.Empty;

    [ObservableProperty]
    private string _lastUpdatedText = "waiting for first sample";

    [ObservableProperty]
    private bool _isRefreshing;

    public string TemperatureNotice => "Temperature sensors check in after the admin handshake.";

    public void ApplyMetrics(SystemMetricSnapshot snapshot)
    {
        Cpu.Apply(snapshot);
        Memory.Apply(snapshot);
        Disk.Apply(snapshot);
        Network.Apply(snapshot);
        LastUpdatedText = $"last tick {snapshot.Timestamp:HH:mm:ss}";
    }

    public void ApplyProcesses(IReadOnlyList<ProcessSample> processes)
    {
        TopProcesses.Clear();
        foreach (var process in processes.Take(6))
        {
            TopProcesses.Add(process);
        }

        HasProcesses = TopProcesses.Count > 0;
    }

    public void ApplyStorage(IReadOnlyList<DriveSnapshot> drives)
    {
        Drives.Clear();
        foreach (var drive in drives.Take(3))
        {
            Drives.Add(drive);
        }

        HasDrives = Drives.Count > 0;
        var hidden = Math.Max(0, drives.Count - Drives.Count);
        MoreDrivesText = hidden > 0 ? $"+{hidden} more on the Storage page" : string.Empty;
        Disk.ApplyStorage(drives);
    }
}
