namespace PulsePal.Models;

public sealed record DriveSnapshot(
    string Name,
    string Label,
    string Format,
    string DriveType,
    long TotalBytes,
    long FreeBytes,
    double UsedPercent,
    bool IsReady)
{
    public string Letter => Name.TrimEnd('\\');

    public string Title => string.IsNullOrWhiteSpace(Label) ? "Local Disk" : Label;

    public string DisplayName => string.IsNullOrWhiteSpace(Label) ? Name.TrimEnd('\\') : $"{Label} ({Name.TrimEnd('\\')})";

    public string FreeText => FormatBytes(FreeBytes);

    public string UsedText => FormatBytes(Math.Max(0, TotalBytes - FreeBytes));

    public string TotalText => FormatBytes(TotalBytes);

    public string UsageText => $"{UsedPercent:0}% full";

    public bool IsNearlyFull => UsedPercent >= 90;

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            return "n/a";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit >= 3 ? $"{value:0.0} {units[unit]}" : $"{value:0} {units[unit]}";
    }
}
