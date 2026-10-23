namespace PulsePal.Models;

/// <summary>A Windows service, flattened into something the table can bind to.</summary>
public sealed record ServiceSnapshot(
    string Name,
    string DisplayName,
    string StatusText,
    string StatusKind,
    string StartTypeText,
    bool IsRunning,
    bool CanStart,
    bool CanStop,
    bool IsProtected)
{
    public string SearchText => $"{DisplayName} {Name} {StatusText} {StartTypeText}";
}
