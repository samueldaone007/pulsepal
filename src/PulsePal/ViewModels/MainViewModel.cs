using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PulsePal.Models;
using PulsePal.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PulsePal.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly Geometry SmileMouth = Freeze(Geometry.Parse("M7.5,13.9 C9.5,16.1 14.4,16.1 16.5,13.7"));
    private static readonly Geometry FlatMouth = Freeze(Geometry.Parse("M8,15 L16,15"));
    private static readonly Geometry WavyMouth = Freeze(Geometry.Parse("M7.5,15 C9,13.7 10.5,15.4 12,14.5 C13.5,13.7 14.8,15.4 16.5,14.2"));
    private static readonly Geometry FrownMouth = Freeze(Geometry.Parse("M7.5,16 C9.5,13.7 14.4,13.7 16.5,16"));

    private static readonly Brush ChillBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x20)));
    private static readonly Brush BusyBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xB8, 0xF2, 0x5B)));
    private static readonly Brush SweatBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x7A, 0x36)));
    private static readonly Brush FireBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x5A, 0x36)));

    private readonly Dispatcher _dispatcher;
    private readonly ISamplingService _samplingService;
    private readonly OverviewViewModel _overview;
    private readonly DispatcherTimer _clockTimer;
    private readonly Dictionary<string, PlaceholderPageViewModel> _placeholderPages = [];
    private double _smoothedLoad = -1;
    private IProcessService _processService;
    private ProcessesViewModel _processesViewModel;

    public MainViewModel(ISamplingService samplingService, OverviewViewModel overview, IProcessService processService)
    {
        _samplingService = samplingService;
        _overview = overview;
        _processService = processService;
        _processesViewModel = new ProcessesViewModel(processService);
        _dispatcher = Dispatcher.CurrentDispatcher;

        NavItems =
        [
            new NavItemViewModel("overview", "Overview", "M3,19 C3,11 7,4.5 12,4.5 C17,4.5 21,10 21,16 L21,19 M2.5,21 L21.5,21 M12,17 L16,10 M7.5,7.5 L9,9 M17,7 L15.8,8.7 M3.8,13 L5.5,13", "The whole patient, one glance."),
            new NavItemViewModel("processes", "Processes", "M4,5.5 C8,4.7 16,5.2 20,4.8 L19.6,9.8 C15,10.2 8,10.1 4.4,9.7 Z M4.3,13.2 C9,12.5 15,13 19.8,12.4 L20.1,17.5 C15,18.1 8,17.8 3.9,17.3 Z M6,7.7 L6.1,7.7 M6.2,15.4 L6.3,15.4", "Everything currently breathing."),
            new NavItemViewModel("performance", "Performance", "M2.5,13.3 L6.3,13.1 L9.1,5.6 L13.1,20 L16.6,9.7 L18.6,13.2 L21.8,12.8", "The last sixty seconds, enlarged."),
            new NavItemViewModel("storage", "Storage", "M4,5.5 C8,4.8 16,4.7 20,5.2 L19.7,10 C16,10.5 8,10.5 4.3,9.9 Z M4.3,13.3 C8,12.7 16,12.7 19.8,13.2 L19.6,18 C16,18.5 8,18.5 4.1,17.9 Z M7,7.8 L7.2,7.8 M7.1,15.7 L7.3,15.7", "Drives, shelves, breathing room."),
            new NavItemViewModel("sensors", "Sensors", "M12,3.2 C10.2,3.2 9.1,4.7 9.1,6.5 L9.1,14.2 C7.6,15.7 7.4,18.4 9,20.1 C10.8,21.9 13.7,21.6 15,19.5 C16,17.9 15.8,15.7 14.5,14.2 L14.5,6.5 C14.5,4.7 13.8,3.2 12,3.2 Z M12,8 L12,16.7 M12,18.1 L12,18.3", "Temperature and fan gossip."),
            new NavItemViewModel("services", "Services", "M3.5,8 C7,7.3 13.8,7.5 20,7.2 L19.8,11.1 C14,11.4 8,11.3 3.3,11.2 Z M14,9.1 L18.5,9.1 M4.2,15.8 C9,15.3 15,15.5 20.3,15.1 L20.1,19 C15,19.4 9,19.4 4,19 Z M5.8,17 L9.9,17", "Background helpers, on and off."),
            new NavItemViewModel("settings", "Settings", "M12,4.3 C15.3,4.3 18.2,7 18.2,10.4 C18.2,13.8 16.8,17 12.1,17 C8.8,17 5.7,14.2 5.7,10.7 C5.7,7.2 8.7,4.3 12,4.3 Z M12,8.1 C10.7,8.1 9.7,9.1 9.7,10.4 C9.7,11.7 10.7,12.6 12,12.6 C13.3,12.6 14.2,11.7 14.2,10.4 C14.2,9.1 13.3,8.1 12,8.1 Z M12,2.5 L12,4.3 M12,17 L12,21 M3.2,10.3 L5.7,10.4 M18.2,10.4 L21,10.5 M5.5,4.3 L7.2,6.1 M16.8,14.5 L18.6,16.3", "Tune the knobs and habits.")
        ];

        _selectedNav = NavItems[0];
        _selectedNav.IsSelected = true;
        _currentPage = _overview;
        _clockText = DateTime.Now.ToString("HH:mm:ss");
        _moodText = "Chill";
        _moodDetail = "nothing is on fire";
        _moodMouth = SmileMouth;
        _moodAccent = ChillBrush;
        _lastTickText = "local readings warming up";
        _statusLine = "LOCAL ONLY  /  NO CLOUD";

        BuildPlaceholderPages();

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => ClockText = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();

        _samplingService.MetricsUpdated += OnMetricsUpdated;
        _samplingService.ProcessesUpdated += OnProcessesUpdated;
        _samplingService.StorageUpdated += OnStorageUpdated;
        _samplingService.SamplingError += OnSamplingError;
    }

    public ObservableCollection<NavItemViewModel> NavItems { get; }

    [ObservableProperty]
    private NavItemViewModel _selectedNav;

    [ObservableProperty]
    private object _currentPage;

    [ObservableProperty]
    private string _clockText;

    [ObservableProperty]
    private string _moodText;

    [ObservableProperty]
    private string _moodDetail;

    [ObservableProperty]
    private Geometry _moodMouth;

    [ObservableProperty]
    private Brush _moodAccent;

    [ObservableProperty]
    private string _lastTickText;

    [ObservableProperty]
    private string _statusLine;

    [RelayCommand]
    private void Navigate(NavItemViewModel? item)
    {
        if (item is null || ReferenceEquals(item, SelectedNav))
        {
            return;
        }

        foreach (var navItem in NavItems)
        {
            navItem.IsSelected = ReferenceEquals(navItem, item);
        }

        SelectedNav = item;
        CurrentPage = item.Key switch
        {
            "overview" => _overview,
            "processes" => _processesViewModel,
            _ when _placeholderPages.TryGetValue(item.Key, out var page) => page,
            _ => _overview
        };
    }

    private void OnMetricsUpdated(SystemMetricSnapshot snapshot)
    {
        _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _overview.ApplyMetrics(snapshot);
            LastTickText = $"last tick {snapshot.Timestamp:HH:mm:ss}";
            UpdateMood(snapshot);
        }));
    }

    private void OnProcessesUpdated(IReadOnlyList<ProcessSample> processes)
    {
        _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _overview.ApplyProcesses(processes);
        }));
    }

    private void OnStorageUpdated(IReadOnlyList<DriveSnapshot> drives)
    {
        _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _overview.ApplyStorage(drives);
        }));
    }

    private void OnSamplingError(string message)
    {
        _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            LastTickText = $"sampling hiccup / {message}";
        }));
    }

    private void UpdateMood(SystemMetricSnapshot snapshot)
    {
        var load = Math.Max(snapshot.CpuPercent ?? 0, snapshot.MemoryPercent ?? 0);
        _smoothedLoad = _smoothedLoad < 0 ? load : _smoothedLoad * 0.72 + load * 0.28;

        if (_smoothedLoad < 45)
        {
            MoodText = "Chill";
            MoodDetail = "nothing is on fire";
            MoodMouth = SmileMouth;
            MoodAccent = ChillBrush;
        }
        else if (_smoothedLoad < 70)
        {
            MoodText = "Busy";
            MoodDetail = "chugging along";
            MoodMouth = FlatMouth;
            MoodAccent = BusyBrush;
        }
        else if (_smoothedLoad < 87)
        {
            MoodText = "Sweating";
            MoodDetail = "the fans are gossiping";
            MoodMouth = WavyMouth;
            MoodAccent = SweatBrush;
        }
        else
        {
            MoodText = "On fire";
            MoodDetail = "call the fire brigade";
            MoodMouth = FrownMouth;
            MoodAccent = FireBrush;
        }
    }

    private void BuildPlaceholderPages()
    {
        _placeholderPages["processes"] = new PlaceholderPageViewModel(
            "Processes",
            "PROCESS BENCH / NEXT UP",
            "The process table is being sanded and sorted. Soon: search, CPU deltas, and a polite confirmation before ending anything.",
            "#FFB020");

        _placeholderPages["performance"] = new PlaceholderPageViewModel(
            "Performance",
            "BIG CHARTS / NEXT UP",
            "The sixty-second charts get the larger room next. Same handful of samples, more elbow room.",
            "#6EC6FF");

        _placeholderPages["storage"] = new PlaceholderPageViewModel(
            "Storage",
            "DRIVE SHELVES / NEXT UP",
            "Every drive will get its own shelf, filesystem sticker, and a clear look at what is left.",
            "#FF6FA8");

        _placeholderPages["sensors"] = new PlaceholderPageViewModel(
            "Sensors",
            "THERMOMETERS / NEXT UP",
            "LibreHardwareMonitor is waiting in the wings. Admin permission decides how much gossip the hardware shares.",
            "#FF5A36");

        _placeholderPages["services"] = new PlaceholderPageViewModel(
            "Services",
            "SERVICE HATCH / NEXT UP",
            "Starting and stopping Windows services is coming, with confirmations and a firm admin gate.",
            "#B8F25B");

        _placeholderPages["settings"] = new PlaceholderPageViewModel(
            "Settings",
            "THE KNOB DRAWER / NEXT UP",
            "Refresh cadence, alert thresholds, Windows startup, and tray behaviour will live here.",
            "#FFB020");
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _samplingService.MetricsUpdated -= OnMetricsUpdated;
        _samplingService.ProcessesUpdated -= OnProcessesUpdated;
        _samplingService.StorageUpdated -= OnStorageUpdated;
        _samplingService.SamplingError -= OnSamplingError;
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
