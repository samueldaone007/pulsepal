using System.Windows;
using PulsePal.Services;
using PulsePal.ViewModels;
using PulsePal.Views;

namespace PulsePal;

public partial class App : Application
{
    private MetricsService? _metricsService;
    private SamplingService? _samplingService;
    private SensorService? _sensorService;
    private MainViewModel? _mainViewModel;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _metricsService = new MetricsService();
        var processService = new ProcessService();
        var storageService = new StorageService();
        var elevationService = new ElevationService();
        _sensorService = new SensorService(elevationService.IsAdministrator);
        _samplingService = new SamplingService(_metricsService, processService, storageService, _sensorService);

        var overview = new OverviewViewModel(_metricsService);
        var performance = new PerformanceViewModel(overview);
        var storage = new StorageViewModel(storageService);
        var dialogService = new DialogService();
        var sensors = new SensorsViewModel(elevationService, dialogService);
        _mainViewModel = new MainViewModel(
            _samplingService,
            overview,
            performance,
            storage,
            sensors,
            processService,
            dialogService);

        _mainWindow = new MainWindow
        {
            DataContext = _mainViewModel
        };

        // Keep the first window fully on screen. WorkArea is already in WPF's
        // device-independent units, so it can be used directly at any DPI scale.
        var workArea = SystemParameters.WorkArea;
        _mainWindow.MinWidth = Math.Min(1120, Math.Max(760, workArea.Width - 24));
        _mainWindow.MinHeight = Math.Min(640, Math.Max(420, workArea.Height - 24));
        _mainWindow.Width = Math.Min(1440, workArea.Width - 24);
        _mainWindow.Height = Math.Min(940, workArea.Height - 24);
        _mainWindow.Left = workArea.Left + (workArea.Width - _mainWindow.Width) / 2;
        _mainWindow.Top = workArea.Top + (workArea.Height - _mainWindow.Height) / 2;

        MainWindow = _mainWindow;
        _mainWindow.Show();
        _samplingService.Start();

        var startupPage = e.Args
            .FirstOrDefault(argument => argument.StartsWith("--page=", StringComparison.OrdinalIgnoreCase))
            ?.Substring("--page=".Length);

        if (!string.IsNullOrWhiteSpace(startupPage))
        {
            _mainViewModel.NavigateTo(startupPage);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.Dispose();
        _samplingService?.Dispose();
        _sensorService?.Dispose();
        _metricsService?.Dispose();
        base.OnExit(e);
    }
}