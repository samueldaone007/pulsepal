using PulsePal.Models;

namespace PulsePal.Services;

public interface IServiceControlService
{
    IReadOnlyList<ServiceSnapshot> Sample();

    bool Start(string serviceName, out string? error);

    bool Stop(string serviceName, out string? error);
}
