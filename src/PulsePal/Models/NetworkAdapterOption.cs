namespace PulsePal.Models;

public sealed record NetworkAdapterOption(
    string Id,
    string Name,
    string Description,
    long SpeedBitsPerSecond,
    bool HasGateway)
{
    public bool IsPhysical =>
        Description.Contains("Ethernet", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("Realtek", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("Intel", StringComparison.OrdinalIgnoreCase);
}
