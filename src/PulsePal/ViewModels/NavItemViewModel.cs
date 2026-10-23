using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace PulsePal.ViewModels;

public partial class NavItemViewModel : ObservableObject
{
    public NavItemViewModel(string key, string label, string iconData, string kicker)
    {
        Key = key;
        Label = label;
        Kicker = kicker;
        Icon = Geometry.Parse(iconData);
        Icon.Freeze();
    }

    public string Key { get; }

    public string Label { get; }

    public string Kicker { get; }

    public Geometry Icon { get; }

    [ObservableProperty]
    private bool _isSelected;
}
