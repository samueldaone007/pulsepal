using System.Runtime.InteropServices;
using System.Windows;
using PulsePal.Services;
using PulsePal.ViewModels;
using PulsePal.Views;

namespace PulsePal;

public partial class App : Application
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    private MetricsService? _metricsService;
    private SamplingService? _samplingService;
    private MainViewModel? _mainViewModel;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _metricsService = new MetricsService();
        var processService = new ProcessService();
        var storageService = new StorageService();
        _samplingService = new SamplingService(_metricsService, processService, storageService);

        var overview = new OverviewViewModel(_metricsService);
        _mainViewModel = new MainViewModel(_samplingService, overview, processService);

        _mainWindow = new MainWindow
        {
            DataContext = _mainViewModel
        };

        // Keep the first window fully on screen, even on a 720p desktop.
        var workArea = SystemParameters.WorkArea;
        // SystemParameters.WorkArea can report physical pixels on high-DPI screens
        // in a per-monitor-aware process, so convert it back to WPF device-independent
        // units before sizing the window. This keeps the window fully on screen at
        // 125%, 150%, and 175% scaling instead of extending past the right/bottom edge.
        var dpiScale = GetDpiForSystem() / 96.0;
        var workWidth = workArea.Width / dpiScale;
        var workHeight = workArea.Height / dpiScale;

        _mainWindow.MinWidth = Math.Min(1000, Math.Max(720, workWidth - 16));
        _mainWindow.MinHeight = Math.Min(600, Math.Max(420, workHeight - 16));
        _mainWindow.Width = Math.Min(1440, workWidth - 16);
        _mainWindow.Height = Math.Min(940, workHeight - 16);
        _mainWindow.Left = workArea.Left / dpiScale + (workWidth - _mainWindow.Width) / 2;
        _mainWindow.Top = workArea.Top / dpiScale + (workHeight - _mainWindow.Height) / 2;

        MainWindow = _mainWindow;
        _mainWindow.Show();
        _samplingService.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.Dispose();
        _samplingService?.Dispose();
        _metricsService?.Dispose();
        base.OnExit(e);
    }
}