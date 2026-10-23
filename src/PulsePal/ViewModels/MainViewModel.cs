using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace PulsePal.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _clockTimer;

    public MainViewModel()
    {
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
        _clockText = DateTime.Now.ToString("HH:mm:ss");
        _moodText = "Chill";
        _moodDetail = "nothing is on fire";
        _lastTickText = "local readings warming up";
        _statusLine = "LOCAL ONLY  /  NO CLOUD";

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => ClockText = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();
    }

    public ObservableCollection<NavItemViewModel> NavItems { get; }

    [ObservableProperty]
    private NavItemViewModel _selectedNav;

    [ObservableProperty]
    private string _clockText;

    [ObservableProperty]
    private string _moodText;

    [ObservableProperty]
    private string _moodDetail;

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
    }

    public void Dispose() => _clockTimer.Stop();
}
