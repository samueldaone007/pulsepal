using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PulsePal.Models;
using PulsePal.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace PulsePal.ViewModels;

public partial class SensorsViewModel : ObservableObject
{
    private readonly IElevationService _elevation;
    private readonly IDialogService _dialogs;

    public SensorsViewModel(IElevationService elevation, IDialogService dialogs)
    {
        _elevation = elevation;
        _dialogs = dialogs;

        IsElevated = elevation.IsAdministrator;
        ShowElevationBanner = !IsElevated;
        StateText = IsElevated ? "ADMIN / DEEP SENSORS" : "STANDARD / LIMITED";
    }

    public ObservableCollection<SensorGroup> Groups { get; } = [];

    [ObservableProperty]
    private bool _hasSensors;

    [ObservableProperty]
    private bool _isElevated;

    [ObservableProperty]
    private bool _showElevationBanner;

    [ObservableProperty]
    private string _stateText = "STANDARD / LIMITED";

    [ObservableProperty]
    private string _statusText = "waiting for the hardware monitor";

    [ObservableProperty]
    private string _lastUpdatedText = "waiting for first read";

    [ObservableProperty]
    private double _hottestCelsius = double.NaN;

    [ObservableProperty]
    private string _hottestDetail = "no temperature channels";

    [ObservableProperty]
    private double _fanRpm = double.NaN;

    [ObservableProperty]
    private string _fanDetail = "no fan channels";

    [ObservableProperty]
    private double _powerWatts = double.NaN;

    [ObservableProperty]
    private string _powerDetail = "no power channels";

    [ObservableProperty]
    private double _liveReadings;

    [ObservableProperty]
    private string _readingsDetail = "waiting for channels";

    public void ApplySnapshot(SensorSnapshot snapshot)
    {
        IsElevated = snapshot.IsElevated;
        ShowElevationBanner = !snapshot.IsElevated;
        StateText = snapshot.IsElevated
            ? snapshot.IsAvailable ? "ADMIN / DEEP SENSORS" : "ADMIN / NO CHANNELS"
            : "STANDARD / LIMITED";
        StatusText = snapshot.Status;
        LastUpdatedText = $"last read {snapshot.Timestamp:HH:mm:ss}";

        Groups.Clear();
        foreach (var group in snapshot.Groups)
        {
            Groups.Add(group);
        }

        HasSensors = Groups.Count > 0;

        var readings = snapshot.Groups.SelectMany(g => g.Readings).ToArray();
        LiveReadings = readings.Length;
        ReadingsDetail = Groups.Count == 0
            ? "waiting for channels"
            : $"{Groups.Count} device{(Groups.Count == 1 ? string.Empty : "s")}";

        var hottest = readings
            .Where(r => r.Kind == "Temperature" && r.NumericValue is not null)
            .OrderByDescending(r => r.NumericValue)
            .FirstOrDefault();

        HottestCelsius = hottest?.NumericValue ?? double.NaN;
        HottestDetail = hottest is null
            ? "no temperature channels"
            : $"{hottest.Hardware} / {hottest.Name}";

        var fans = readings
            .Where(r => r.Kind == "Fan" && r.NumericValue is not null)
            .ToArray();

        FanRpm = fans.Length > 0 ? fans.Sum(r => r.NumericValue!.Value) : double.NaN;
        FanDetail = fans.Length == 0
            ? "no fan channels"
            : $"{fans.Length} fan channel{(fans.Length == 1 ? string.Empty : "s")}";

        var power = readings
            .Where(r => r.Kind == "Power" && r.NumericValue is > 0)
            .OrderByDescending(r => r.NumericValue)
            .FirstOrDefault();

        PowerWatts = power?.NumericValue ?? double.NaN;
        PowerDetail = power is null
            ? "no power channels"
            : $"{power.Hardware} / {power.Name}";
    }

    [RelayCommand]
    private void Elevate()
    {
        if (IsElevated || _elevation.IsAdministrator)
        {
            _dialogs.ShowMessage(
                "PULSEPAL / ELEVATION",
                "Already running deep",
                "PulsePal already has administrator access. The deeper hardware channels are already on the menu.",
                "Nice");
            return;
        }

        var confirmed = _dialogs.Confirm(
            "PULSEPAL / ELEVATION",
            "Restart with deeper eyes?",
            "Windows will ask for administrator permission. PulsePal will close and reopen on the Sensors page so the hardware monitor can read temperatures, fans, and voltages.",
            "Restart elevated",
            "Not now");

        if (!confirmed)
        {
            return;
        }

        if (_elevation.TryRestartElevated("sensors", out var error))
        {
            Application.Current?.Shutdown();
            return;
        }

        _dialogs.ShowMessage(
            "PULSEPAL / NO CHANGE",
            "Windows kept the door shut",
            error ?? "The administrator request did not go through. PulsePal is still running in standard mode.",
            "Okay");
    }
}
