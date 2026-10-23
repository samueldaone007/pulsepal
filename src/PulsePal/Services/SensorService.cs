using LibreHardwareMonitor.Hardware;
using PulsePal.Models;

namespace PulsePal.Services;

/// <summary>
/// Opens LibreHardwareMonitor lazily on the sampling thread. Any driver or
/// permission failure is turned into a friendly status instead of a crash.
/// </summary>
public sealed class SensorService : ISensorService
{
    private readonly object _sync = new();
    private readonly bool _isElevated;

    private Computer? _computer;
    private bool _attempted;
    private bool _disposed;
    private string _status = "waiting for the hardware monitor";

    public SensorService(bool isElevated)
    {
        _isElevated = isElevated;
    }

    public SensorSnapshot Sample()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return Empty("sensor service stopped");
            }

            if (!_attempted)
            {
                Initialize();
            }

            if (_computer is null)
            {
                return Empty(_status);
            }

            var groups = new List<SensorGroup>();
            var allReadings = new List<SensorReading>();

            foreach (var hardware in _computer.Hardware)
            {
                Collect(hardware, groups, allReadings);
            }

            if (allReadings.Count == 0)
            {
                var emptyStatus = _isElevated
                    ? "No sensor channels reported. This hardware may not expose them."
                    : "No sensor channels reported yet. Administrator access may unlock temperatures, fans, and voltages.";
                return new SensorSnapshot(DateTime.Now, false, _isElevated, emptyStatus, []);
            }

            var orderedGroups = groups
                .OrderBy(g => HardwarePriority(g.HardwareType))
                .ThenBy(g => g.Hardware, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _status = $"{allReadings.Count} channels from {orderedGroups.Length} device{(orderedGroups.Length == 1 ? string.Empty : "s")}.";
            return new SensorSnapshot(DateTime.Now, true, _isElevated, _status, orderedGroups);
        }
    }

    private void Initialize()
    {
        _attempted = true;
        Computer? computer = null;

        try
        {
            computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = false
            };

            computer.Open();
            _computer = computer;
            _status = _isElevated ? "hardware monitor ready" : "hardware monitor ready in standard mode";
        }
        catch (Exception ex)
        {
            _status = _isElevated
                ? $"sensor bridge unavailable: {ex.Message}"
                : "sensor bridge locked. Restart elevated to unlock hardware readings.";

            if (computer is not null)
            {
                try
                {
                    computer.Close();
                }
                catch
                {
                    // A failed open can leave the driver half-closed; shutdown must not throw.
                }
            }
        }
    }

    private void Collect(IHardware hardware, List<SensorGroup> groups, List<SensorReading> allReadings)
    {
        try
        {
            hardware.Update();
        }
        catch
        {
            // Individual devices can disappear or reject a read; keep the rest.
        }

        var readings = new List<SensorReading>();
        var hardwareType = FriendlyHardwareType(hardware.HardwareType.ToString());

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not float value ||
                float.IsNaN(value) ||
                float.IsInfinity(value) ||
                !IsWanted(sensor.SensorType.ToString()))
            {
                continue;
            }

            var kind = sensor.SensorType.ToString();
            var reading = new SensorReading(
                hardware.Name,
                hardwareType,
                sensor.Name,
                kind,
                value,
                Format(kind, value));

            readings.Add(reading);
            allReadings.Add(reading);
        }

        if (readings.Count > 0)
        {
            groups.Add(new SensorGroup(
                hardware.Name,
                hardwareType,
                readings
                    .OrderBy(r => SensorPriority(r.Kind))
                    .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray()));
        }

        foreach (var child in hardware.SubHardware)
        {
            Collect(child, groups, allReadings);
        }
    }

    private SensorSnapshot Empty(string status)
        => new(DateTime.Now, false, _isElevated, status, []);

    private static bool IsWanted(string kind)
        => kind is "Temperature" or "Fan" or "Power" or "Voltage" or "Load" or
            "Clock" or "Data" or "Throughput" or "Level" or "Frequency" or "Factor";

    private static string Format(string kind, double value) => kind switch
    {
        "Temperature" => $"{value:0.0} °C",
        "Fan" => $"{value:0} RPM",
        "Power" => $"{value:0.0} W",
        "Voltage" => $"{value:0.000} V",
        "Load" or "Level" => $"{value:0.0} %",
        "Clock" => $"{value:0} MHz",
        "Data" => $"{value:0.0} GB",
        "Throughput" => $"{value:0.0} MB/s",
        "Frequency" => $"{value:0.0} Hz",
        "Factor" => $"{value:0.0}x",
        _ => $"{value:0.0}"
    };

    private static int SensorPriority(string kind) => kind switch
    {
        "Temperature" => 0,
        "Fan" => 1,
        "Power" => 2,
        "Voltage" => 3,
        "Load" => 4,
        "Clock" => 5,
        "Data" => 6,
        "Throughput" => 7,
        "Level" => 8,
        "Frequency" => 9,
        "Factor" => 10,
        _ => 11
    };

    private static int HardwarePriority(string hardwareType) => hardwareType switch
    {
        "CPU" => 0,
        "GPU" => 1,
        "Mainboard" => 2,
        "Memory" => 3,
        "Storage" => 4,
        _ => 5
    };

    private static string FriendlyHardwareType(string hardwareType) => hardwareType switch
    {
        "Cpu" => "CPU",
        "GpuNvidia" or "GpuAmd" or "GpuIntel" => "GPU",
        "Motherboard" => "Mainboard",
        "EmbeddedController" => "Embedded",
        _ => hardwareType
    };

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _computer?.Close();
            }
            catch
            {
                // Shutdown must stay quiet even if the driver is already gone.
            }

            _computer = null;
        }
    }
}
