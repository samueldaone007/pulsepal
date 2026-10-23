using PulsePal.Models;

namespace PulsePal.Services;

public interface ISamplingService : IDisposable
{
    event Action<SystemMetricSnapshot>? MetricsUpdated;
    event Action<IReadOnlyList<ProcessSample>>? ProcessesUpdated;
    event Action<IReadOnlyList<DriveSnapshot>>? StorageUpdated;
    event Action<SensorSnapshot>? SensorsUpdated;
    event Action<string>? SamplingError;

    bool IsRunning { get; }

    void Start();
    void Stop();
}
