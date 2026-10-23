using System.Windows;
using System.Windows.Controls;

namespace PulsePal.Views;

public partial class OverviewPage : UserControl
{
    public OverviewPage()
    {
        InitializeComponent();
    }

    private void PageScroll_Loaded(object sender, RoutedEventArgs e) => PageScroll.ScrollToTop();
}