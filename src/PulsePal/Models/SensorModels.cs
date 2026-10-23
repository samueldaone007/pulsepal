namespace PulsePal.Models;

/// <summary>A single channel reported by the hardware monitor.</summary>
public sealed record SensorReading(
    string Hardware,
    string HardwareType,
    string Name,
    string Kind,
    double? NumericValue,
    string ValueText)
{
    public string TypeLabel => Kind switch
    {
        "Temperature" => "TEMP",
        "Fan" => "FAN",
        "Power" => "POWER",
        "Voltage" => "VOLT",
        "Load" => "LOAD",
        "Clock" => "CLOCK",
        "Data" => "DATA",
        "Throughput" => "FLOW",
        "Level" => "LEVEL",
        "Frequency" => "FREQ",
        "Factor" => "FACTOR",
        _ => Kind.ToUpperInvariant()
    };

    public bool IsHot => Kind == "Temperature" && NumericValue >= 80;
}

/// <summary>All readable channels for one physical device.</summary>
public sealed record SensorGroup(
    string Hardware,
    string HardwareType,
    IReadOnlyList<SensorReading> Readings)
{
    public string ReadingCountText => Readings.Count == 1 ? "1 channel" : $"{Readings.Count} channels";
}

/// <summary>One hardware-monitor sweep, safe to hand to the UI thread.</summary>
public sealed record SensorSnapshot(
    DateTime Timestamp,
    bool IsAvailable,
    bool IsElevated,
    string Status,
    IReadOnlyList<SensorGroup> Groups);
