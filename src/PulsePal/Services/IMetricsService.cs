using PulsePal.Models;

namespace PulsePal.Services;

public interface IMetricsService : IDisposable
{
    SystemMetricSnapshot Sample();

    IReadOnlyList<NetworkAdapterOption> GetNetworkAdapters();

    void SelectNetworkAdapter(string? adapterId);
}
