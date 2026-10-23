using CommunityToolkit.Mvvm.ComponentModel;
using PulsePal.Models;
using PulsePal.Services;
using System.Collections.ObjectModel;

namespace PulsePal.ViewModels;

public partial class StorageViewModel : ObservableObject
{
    public StorageViewModel(IStorageService storageService)
    {
        ApplyStorage(storageService.Sample());
    }

    public ObservableCollection<DriveSnapshot> Drives { get; } = [];

    [ObservableProperty]
    private bool _hasDrives;

    [ObservableProperty]
    private string _lastUpdatedText = "waiting for a drive scan";

    [ObservableProperty]
    private string _driveCountText = "0 drives";

    [ObservableProperty]
    private double _totalUsedPercent;

    [ObservableProperty]
    private string _totalCapacityText = "n/a";

    [ObservableProperty]
    private string _totalUsedText = "n/a";

    [ObservableProperty]
    private string _totalFreeText = "n/a";

    [ObservableProperty]
    private string _summaryNote = "no shelves to inspect";

    [ObservableProperty]
    private string _tightestDriveText = "No fixed or removable drives answered the roll call.";

    public void ApplyStorage(IReadOnlyList<DriveSnapshot> drives)
    {
        var ordered = drives
            .OrderBy(d => string.Equals(d.DriveType, "Fixed", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(d => d.Letter, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Drives.Clear();
        foreach (var drive in ordered)
        {
            Drives.Add(drive);
        }

        HasDrives = Drives.Count > 0;
        DriveCountText = Drives.Count == 1 ? "1 drive" : $"{Drives.Count} drives";
        LastUpdatedText = $"last scan {DateTime.Now:HH:mm:ss}";

        var total = drives.Sum(d => d.TotalBytes);
        var free = drives.Sum(d => d.FreeBytes);
        var used = Math.Max(0, total - free);

        TotalUsedPercent = total > 0 ? Math.Clamp(used * 100d / total, 0, 100) : 0;
        TotalCapacityText = DriveSnapshot.FormatBytes(total);
        TotalUsedText = DriveSnapshot.FormatBytes(used);
        TotalFreeText = DriveSnapshot.FormatBytes(free);

        var tightest = drives.OrderByDescending(d => d.UsedPercent).FirstOrDefault();
        if (tightest is null)
        {
            SummaryNote = "no shelves to inspect";
            TightestDriveText = "No fixed or removable drives answered the roll call.";
            return;
        }

        TightestDriveText = $"{tightest.DisplayName} is {tightest.UsageText.ToLowerInvariant()}. {tightest.FreeText} still free.";
        SummaryNote = tightest.UsedPercent >= 90
            ? "one shelf is asking for a cleanup"
            : tightest.UsedPercent >= 75
                ? "the shelves are getting cozy"
                : "plenty of shelf space";
    }
}
