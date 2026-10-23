using PulsePal.Models;

namespace PulsePal.ViewModels;

public partial class DiskCardViewModel : MetricCardViewModel
{
    private string _volumeSummary = "waiting for drives";

    public DiskCardViewModel()
    {
        ValueUnit = "%";
    }

    public void Apply(SystemMetricSnapshot snapshot)
    {
        if (snapshot.DiskActivityPercent is double activity)
        {
            Value = activity;
            Push(activity);
            IsAlert = activity >= 90;
        }
        else
        {
            Value = double.NaN;
            IsAlert = false;
        }

        Detail = snapshot.DiskReadBytesPerSecond is null && snapshot.DiskWriteBytesPerSecond is null
            ? "I/O counters n/a"
            : $"R {FormatRate(snapshot.DiskReadBytesPerSecond)} · W {FormatRate(snapshot.DiskWriteBytesPerSecond)}";

        SecondaryDetail = _volumeSummary;
    }

    public void ApplyStorage(IReadOnlyList<DriveSnapshot> drives)
    {
        var primary = drives.FirstOrDefault(d => d.Name.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
                      ?? drives.FirstOrDefault();

        _volumeSummary = primary is null
            ? "no fixed drive found"
            : $"{primary.Name.TrimEnd('\\')} is {primary.UsageText.ToLowerInvariant()}";

        SecondaryDetail = _volumeSummary;
    }

    private static string FormatRate(double? bytesPerSecond)
    {
        if (bytesPerSecond is null)
        {
            return "n/a";
        }

        var value = Math.Max(0, bytesPerSecond.Value);
        string[] units = ["B/s", "KB/s", "MB/s", "GB/s"];
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit >= 2 ? $"{value:0.0} {units[unit]}" : $"{value:0} {units[unit]}";
    }
}
