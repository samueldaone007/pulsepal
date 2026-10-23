using PulsePal.Models;
using System.ComponentModel;
using System.ServiceProcess;

namespace PulsePal.Services;

/// <summary>
/// Reads the local service table and asks Windows to start or stop one service.
/// Every call returns a friendly error instead of throwing into the UI.
/// </summary>
public sealed class ServiceControlService : IServiceControlService
{
    private static readonly HashSet<string> ProtectedServiceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "RpcSs", "DcomLaunch", "LSM", "Power", "PlugPlay", "BrokerInfrastructure",
        "SamSs", "Winmgmt", "EventLog", "Schedule", "Themes", "AudioSrv",
        "CryptSvc", "Dhcp", "Dnscache", "WinDefend", "mpssvc", "BFE", "gpsvc"
    };

    public IReadOnlyList<ServiceSnapshot> Sample()
    {
        ServiceController[] controllers;
        try
        {
            controllers = ServiceController.GetServices();
        }
        catch
        {
            return [];
        }

        var services = new List<ServiceSnapshot>(controllers.Length);

        foreach (var controller in controllers)
        {
            using (controller)
            {
                try
                {
                    var name = controller.ServiceName;
                    var displayName = string.IsNullOrWhiteSpace(controller.DisplayName)
                        ? name
                        : controller.DisplayName;
                    var status = controller.Status;
                    var startType = controller.StartType;
                    var isProtected = ProtectedServiceNames.Contains(name);
                    var statusText = FriendlyStatus(status);
                    var statusKind = StatusKind(status);

                    var canStart = !isProtected &&
                                   status == ServiceControllerStatus.Stopped &&
                                   startType != ServiceStartMode.Disabled;

                    var canStop = !isProtected &&
                                  status is ServiceControllerStatus.Running or ServiceControllerStatus.Paused &&
                                  controller.CanStop;

                    services.Add(new ServiceSnapshot(
                        name,
                        displayName,
                        statusText,
                        statusKind,
                        FriendlyStartType(startType),
                        status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending
                            or ServiceControllerStatus.ContinuePending or ServiceControllerStatus.Paused,
                        canStart,
                        canStop,
                        isProtected));
                }
                catch
                {
                    // A service can disappear between enumeration and inspection.
                }
            }
        }

        return services
            .OrderBy(service => service.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public bool Start(string serviceName, out string? error)
    {
        error = null;

        try
        {
            using var controller = new ServiceController(serviceName);
            controller.Refresh();

            if (controller.Status == ServiceControllerStatus.Running)
            {
                return true;
            }

            if (controller.Status is ServiceControllerStatus.StartPending or ServiceControllerStatus.ContinuePending)
            {
                controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(12));
                return true;
            }

            if (controller.Status != ServiceControllerStatus.Stopped)
            {
                error = $"Windows reports this service as {FriendlyStatus(controller.Status)} right now.";
                return false;
            }

            controller.Start();
            controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(12));
            return true;
        }
        catch (System.TimeoutException)
        {
            error = "Windows accepted the request, but the service has not reported Running yet. Refresh in a moment.";
            return false;
        }
        catch (Exception ex)
        {
            error = FriendlyError(ex);
            return false;
        }
    }

    public bool Stop(string serviceName, out string? error)
    {
        error = null;

        try
        {
            using var controller = new ServiceController(serviceName);
            controller.Refresh();

            if (controller.Status == ServiceControllerStatus.Stopped)
            {
                return true;
            }

            if (controller.Status == ServiceControllerStatus.StopPending)
            {
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(12));
                return true;
            }

            if (controller.Status is not (ServiceControllerStatus.Running or ServiceControllerStatus.Paused))
            {
                error = $"Windows reports this service as {FriendlyStatus(controller.Status)} right now.";
                return false;
            }

            if (!controller.CanStop)
            {
                error = "Windows says this service does not accept a stop request.";
                return false;
            }

            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(12));
            return true;
        }
        catch (System.TimeoutException)
        {
            error = "Windows accepted the request, but the service has not reported Stopped yet. Refresh in a moment.";
            return false;
        }
        catch (Exception ex)
        {
            error = FriendlyError(ex);
            return false;
        }
    }

    private static string FriendlyStatus(ServiceControllerStatus status) => status switch
    {
        ServiceControllerStatus.Stopped => "Stopped",
        ServiceControllerStatus.StartPending => "Starting",
        ServiceControllerStatus.StopPending => "Stopping",
        ServiceControllerStatus.Running => "Running",
        ServiceControllerStatus.ContinuePending => "Resuming",
        ServiceControllerStatus.PausePending => "Pausing",
        ServiceControllerStatus.Paused => "Paused",
        _ => status.ToString()
    };

    private static string StatusKind(ServiceControllerStatus status) => status switch
    {
        ServiceControllerStatus.Running => "running",
        ServiceControllerStatus.Stopped => "stopped",
        ServiceControllerStatus.Paused => "paused",
        ServiceControllerStatus.StartPending or ServiceControllerStatus.StopPending
            or ServiceControllerStatus.ContinuePending or ServiceControllerStatus.PausePending => "pending",
        _ => "unknown"
    };

    private static string FriendlyStartType(ServiceStartMode mode) => mode switch
    {
        ServiceStartMode.Boot => "Boot",
        ServiceStartMode.System => "System",
        ServiceStartMode.Automatic => "Automatic",
        ServiceStartMode.Manual => "Manual",
        ServiceStartMode.Disabled => "Disabled",
        _ => mode.ToString()
    };

    private static string FriendlyError(Exception exception)
    {
        var root = exception.GetBaseException();

        if (root is Win32Exception win32)
        {
            return win32.NativeErrorCode switch
            {
                5 => "Windows denied the change. Restart PulsePal elevated and try again.",
                1058 => "This service is disabled and cannot be started.",
                1060 => "The service does not exist.",
                1061 => "The service cannot accept control messages right now.",
                1062 => "The service is not started.",
                1063 => "The service cannot be stopped because it has dependent services.",
                _ => win32.Message
            };
        }

        if (root is UnauthorizedAccessException)
        {
            return "Windows denied the change. Restart PulsePal elevated and try again.";
        }

        return root.Message;
    }
}
