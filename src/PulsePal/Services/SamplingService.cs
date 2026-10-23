using PulsePal.Models;

namespace PulsePal.Services;

/// <summary>
/// Owns the background cadence. No service method here touches a WPF dispatcher;
/// subscribers marshal results to the UI themselves.
/// </summary>
public sealed class SamplingService : ISamplingService
{
    private readonly IMetricsService _metricsService;
    private readonly IProcessService _processService;
    private readonly IStorageService _storageService;
    private readonly ISensorService _sensorService;
    private readonly object _gate = new();

    private CancellationTokenSource? _cancellation;
    private Task[] _workers = [];
    private int _metricsSeconds = 1;
    private int _processSeconds = 3;
    private int _storageSeconds = 5;
    private int _sensorSeconds = 3;
    private string? _lastError;
    private DateTime _lastErrorAt;
    private bool _disposed;

    public SamplingService(
        IMetricsService metricsService,
        IProcessService processService,
        IStorageService storageService,
        ISensorService sensorService)
    {
        _metricsService = metricsService;
        _processService = processService;
        _storageService = storageService;
        _sensorService = sensorService;
    }

    public event Action<SystemMetricSnapshot>? MetricsUpdated;

    public event Action<IReadOnlyList<ProcessSample>>? ProcessesUpdated;

    public event Action<IReadOnlyList<DriveSnapshot>>? StorageUpdated;

    public event Action<SensorSnapshot>? SensorsUpdated;

    public event Action<string>? SamplingError;

    public bool IsRunning { get; private set; }

    public void Start()
    {
        lock (_gate)
        {
            if (IsRunning || _disposed)
            {
                return;
            }

            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;
            IsRunning = true;

            _workers =
            [
                Task.Run(() => LoopAsync(MetricsTickAsync, () => Volatile.Read(ref _metricsSeconds), token)),
                Task.Run(() => LoopAsync(ProcessesTickAsync, () => Volatile.Read(ref _processSeconds), token)),
                Task.Run(() => LoopAsync(StorageTickAsync, () => Volatile.Read(ref _storageSeconds), token)),
                Task.Run(() => LoopAsync(SensorsTickAsync, () => Volatile.Read(ref _sensorSeconds), token))
            ];
        }
    }

    public void Stop()
    {
        Task[] workers;
        lock (_gate)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            _cancellation?.Cancel();
            workers = _workers;
            _workers = [];
            _cancellation?.Dispose();
            _cancellation = null;
        }

        try
        {
            Task.WaitAll(workers, TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Background loops tolerate cancellation; shutdown must not block.
        }
    }

    /// <summary>Used by Settings when the user changes the headline refresh cadence.</summary>
    public void UpdateIntervals(int metricsSeconds, int processSeconds = 3)
    {
        Volatile.Write(ref _metricsSeconds, Math.Clamp(metricsSeconds, 1, 10));
        Volatile.Write(ref _processSeconds, Math.Clamp(processSeconds, 1, 30));
    }

    private async Task LoopAsync(Func<Task> tick, Func<int> intervalSeconds, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await tick();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                RaiseError(ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds()), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private Task MetricsTickAsync()
    {
        var snapshot = _metricsService.Sample();
        Raise(MetricsUpdated, snapshot);
        return Task.CompletedTask;
    }

    private Task ProcessesTickAsync()
    {
        var samples = _processService.Sample();
        Raise(ProcessesUpdated, samples);
        return Task.CompletedTask;
    }

    private Task StorageTickAsync()
    {
        var drives = _storageService.Sample();
        Raise(StorageUpdated, drives);
        return Task.CompletedTask;
    }

    private Task SensorsTickAsync()
    {
        var snapshot = _sensorService.Sample();
        Raise(SensorsUpdated, snapshot);
        return Task.CompletedTask;
    }

    private void Raise<T>(Action<T>? handlers, T value)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Action<T>>())
        {
            try
            {
                handler(value);
            }
            catch (Exception ex)
            {
                RaiseError(ex.Message);
            }
        }
    }

    private void RaiseError(string message)
    {
        var now = DateTime.UtcNow;
        if (string.Equals(message, _lastError, StringComparison.Ordinal) &&
            now - _lastErrorAt < TimeSpan.FromSeconds(10))
        {
            return;
        }

        _lastError = message;
        _lastErrorAt = now;
        SamplingError?.Invoke(message);
    }

    public void Dispose()
    {
        Stop();
        _disposed = true;
    }
}
