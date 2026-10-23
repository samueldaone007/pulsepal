using CommunityToolkit.Mvvm.ComponentModel;
using PulsePal.Controls;
using System.Globalization;

namespace PulsePal.ViewModels;

public partial class MetricCardViewModel : ObservableObject
{
    public RollingHistory History { get; } = new(60);

    public string StatUnit { get; init; } = "%";

    [ObservableProperty]
    private double _value = double.NaN;

    [ObservableProperty]
    private string _valueUnit = string.Empty;

    [ObservableProperty]
    private string _valueFormat = "0";

    [ObservableProperty]
    private string _detail = "warming up";

    [ObservableProperty]
    private string _secondaryDetail = string.Empty;

    [ObservableProperty]
    private bool _isAlert;

    [ObservableProperty]
    private string _statsSummary = "avg n/a  ·  peak n/a  ·  low n/a";

    protected void Push(double value)
    {
        History.Push(value);
        RecalculateStats();
    }

    private void RecalculateStats()
    {
        if (History.Count == 0)
        {
            StatsSummary = "avg n/a  ·  peak n/a  ·  low n/a";
            return;
        }

        StatsSummary = string.Create(
            CultureInfo.CurrentCulture,
            $"avg {History.Average():0.0}{StatUnit}  ·  peak {History.Max():0.0}{StatUnit}  ·  low {History.Min():0.0}{StatUnit}");
    }
}
