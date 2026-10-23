using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace PulsePal.Services;

public interface IElevationService
{
    bool IsAdministrator { get; }

    bool TryRestartElevated(string pageKey, out string? error);
}

/// <summary>
/// Starts a second copy of PulsePal through Windows' UAC prompt. The caller
/// shuts the current copy down only after the elevated process actually starts.
/// </summary>
public sealed class ElevationService : IElevationService
{
    public ElevationService()
    {
        IsAdministrator = CheckAdministrator();
    }

    public bool IsAdministrator { get; }

    public bool TryRestartElevated(string pageKey, out string? error)
    {
        error = null;

        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
        {
            error = "PulsePal could not find its own executable to restart.";
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = $"--page={pageKey}",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = true,
                Verb = "runas"
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                error = "Windows did not start the elevated copy.";
                return false;
            }

            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            error = "Windows cancelled the administrator request.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool CheckAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
