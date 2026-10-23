using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PulsePal.Models;
using PulsePal.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace PulsePal.ViewModels;

public partial class ServicesViewModel : ObservableObject
{
    private readonly IServiceControlService _serviceControl;
    private readonly IElevationService _elevation;
    private readonly IDialogService _dialogs;
    private readonly ListCollectionView _itemsView;

    public ServicesViewModel(
        IServiceControlService serviceControl,
        IElevationService elevation,
        IDialogService dialogs)
    {
        _serviceControl = serviceControl;
        _elevation = elevation;
        _dialogs = dialogs;
        IsAdministrator = elevation.IsAdministrator;
        ShowElevationBanner = !IsAdministrator;
        StateText = IsAdministrator ? "ADMIN / CONTROL" : "STANDARD / READ ONLY";

        _itemsView = (ListCollectionView)CollectionViewSource.GetDefaultView(Services);
        _itemsView.Filter = MatchesSearch;
        _itemsView.SortDescriptions.Add(new SortDescription(
            nameof(ServiceSnapshot.DisplayName),
            ListSortDirection.Ascending));

        Apply(_serviceControl.Sample());
    }

    public ObservableCollection<ServiceSnapshot> Services { get; } = [];

    public ICollectionView ItemsView => _itemsView;

    public bool IsAdministrator { get; }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopSelectedCommand))]
    private ServiceSnapshot? _selectedService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopSelectedCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _showElevationBanner;

    [ObservableProperty]
    private string _stateText = "STANDARD / READ ONLY";

    [ObservableProperty]
    private string _lastUpdatedText = "waiting for first scan";

    [ObservableProperty]
    private string _statusText = "scanning services";

    [ObservableProperty]
    private string _actionHint = "Select a service to see its controls.";

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _visibleCount;

    [ObservableProperty]
    private int _runningCount;

    partial void OnSearchTextChanged(string value)
    {
        _itemsView.Refresh();
        RefreshCounts();
    }

    partial void OnSelectedServiceChanged(ServiceSnapshot? value)
    {
        UpdateActionHint();
    }

    partial void OnIsBusyChanged(bool value)
    {
        if (value)
        {
            ActionHint = "Talking to Windows about that service...";
        }
        else
        {
            UpdateActionHint();
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var services = await Task.Run(_serviceControl.Sample);
            Apply(services);
        }
        catch (Exception ex)
        {
            ActionHint = $"The service table did not answer: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartSelected))]
    private async Task StartSelectedAsync()
    {
        var service = SelectedService;
        if (service is null || !CanStartSelected())
        {
            return;
        }

        var confirmed = _dialogs.Confirm(
            "PULSEPAL / SERVICE HATCH",
            $"Start {service.DisplayName}?",
            "Windows services can change how other apps behave. PulsePal will ask Windows to start this one and wait for it to report Running.",
            "Start service",
            "Leave it");

        if (!confirmed)
        {
            return;
        }

        await RunActionAsync(service, start: true);
    }

    [RelayCommand(CanExecute = nameof(CanStopSelected))]
    private async Task StopSelectedAsync()
    {
        var service = SelectedService;
        if (service is null || !CanStopSelected())
        {
            return;
        }

        var confirmed = _dialogs.Confirm(
            "PULSEPAL / SERVICE HATCH",
            $"Stop {service.DisplayName}?",
            "Stopping a service can interrupt other apps or Windows features. PulsePal will ask Windows to stop this one and wait for it to report Stopped.",
            "Stop service",
            "Leave it running");

        if (!confirmed)
        {
            return;
        }

        await RunActionAsync(service, start: false);
    }

    [RelayCommand]
    private void Elevate()
    {
        if (IsAdministrator || _elevation.IsAdministrator)
        {
            _dialogs.ShowMessage(
                "PULSEPAL / ELEVATION",
                "Already running deep",
                "PulsePal already has administrator access. The service hatch is already unlocked.",
                "Nice");
            return;
        }

        var confirmed = _dialogs.Confirm(
            "PULSEPAL / ELEVATION",
            "Restart with a hand on the switch?",
            "Windows will ask for administrator permission. PulsePal will close and reopen on the Services page so it can start and stop local services.",
            "Restart elevated",
            "Stay read-only");

        if (!confirmed)
        {
            return;
        }

        if (_elevation.TryRestartElevated("services", out var error))
        {
            Application.Current?.Shutdown();
            return;
        }

        _dialogs.ShowMessage(
            "PULSEPAL / NO CHANGE",
            "Windows kept the door shut",
            error ?? "The administrator request did not go through. PulsePal is still in read-only mode.",
            "Okay");
    }

    private bool CanStartSelected()
        => !IsBusy && IsAdministrator && SelectedService is { CanStart: true };

    private bool CanStopSelected()
        => !IsBusy && IsAdministrator && SelectedService is { CanStop: true };

    private async Task RunActionAsync(ServiceSnapshot service, bool start)
    {
        IsBusy = true;
        ActionHint = $"{(start ? "Starting" : "Stopping")} {service.DisplayName}...";

        try
        {
            string? error = null;
            var success = start
                ? await Task.Run(() => _serviceControl.Start(service.Name, out error))
                : await Task.Run(() => _serviceControl.Stop(service.Name, out error));

            var services = await Task.Run(_serviceControl.Sample);
            Apply(services);

            if (success)
            {
                ActionHint = $"{service.DisplayName} is {(start ? "running" : "stopped")}.";
            }
            else
            {
                ActionHint = error ?? "Windows did not make the change.";
                _dialogs.ShowMessage(
                    "PULSEPAL / SERVICE HATCH",
                    "Windows held the switch",
                    error ?? "The service did not change state. Refresh in a moment and check Windows services.",
                    "Okay");
            }
        }
        catch (Exception ex)
        {
            ActionHint = $"The service action failed: {ex.Message}";
            _dialogs.ShowMessage(
                "PULSEPAL / SERVICE HATCH",
                "The hatch jammed",
                ex.Message,
                "Okay");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Apply(IReadOnlyList<ServiceSnapshot> services)
    {
        var selectedName = SelectedService?.Name;

        Services.Clear();
        foreach (var service in services)
        {
            Services.Add(service);
        }

        _itemsView.Refresh();

        SelectedService = selectedName is null
            ? null
            : Services.FirstOrDefault(service => string.Equals(service.Name, selectedName, StringComparison.OrdinalIgnoreCase));

        TotalCount = Services.Count;
        RunningCount = Services.Count(service => service.IsRunning);
        LastUpdatedText = $"last scan {DateTime.Now:HH:mm:ss}";
        RefreshCounts();
        UpdateActionHint();
    }

    private void RefreshCounts()
    {
        VisibleCount = _itemsView.Cast<object>().Count();
        StatusText = $"{VisibleCount} shown / {TotalCount} services / {RunningCount} running";
    }

    private bool MatchesSearch(object item)
    {
        if (item is not ServiceSnapshot service)
        {
            return false;
        }

        var query = SearchText.Trim();
        return query.Length == 0 ||
               service.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private void UpdateActionHint()
    {
        if (!IsAdministrator)
        {
            ActionHint = "Read-only mode. Restart elevated to start or stop services.";
            return;
        }

        if (SelectedService is null)
        {
            ActionHint = "Select a service to see its controls.";
            return;
        }

        if (SelectedService.IsProtected)
        {
            ActionHint = "This service is on PulsePal's do-not-touch list.";
            return;
        }

        if (SelectedService.CanStart)
        {
            ActionHint = $"Ready to start {SelectedService.DisplayName}.";
            return;
        }

        if (SelectedService.CanStop)
        {
            ActionHint = $"Ready to stop {SelectedService.DisplayName}.";
            return;
        }

        ActionHint = "Windows will not let PulsePal change this service right now.";
    }
}
