using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace PulsePal.ViewModels;

public partial class PlaceholderPageViewModel : ObservableObject
{
    public PlaceholderPageViewModel(string title, string kicker, string note, string accentHex)
    {
        Title = title;
        Kicker = kicker;
        Note = note;
        var color = (Color)ColorConverter.ConvertFromString(accentHex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Accent = brush;
    }

    public string Title { get; }

    public string Kicker { get; }

    public string Note { get; }

    public Brush Accent { get; }
}
