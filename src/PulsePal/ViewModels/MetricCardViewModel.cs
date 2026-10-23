using CommunityToolkit.Mvvm.ComponentModel;
using PulsePal.Controls;

namespace PulsePal.ViewModels;

public partial class MetricCardViewModel : ObservableObject
{
    public RollingHistory History { get; } = new(60);

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

    protected void Push(double value) => History.Push(value);
}
