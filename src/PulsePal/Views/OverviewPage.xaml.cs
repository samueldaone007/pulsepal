using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PulsePal.Views;

public partial class OverviewPage : UserControl
{
    public OverviewPage()
    {
        InitializeComponent();
    }

    private void PageScroll_Loaded(object sender, RoutedEventArgs e)
    {
        PageScroll.ScrollToTop();
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            try
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "pulsepal-layout.txt"),
                    $"window={Window.GetWindow(this)?.ActualWidth:0.#} page={ActualWidth:0.#} scroll={PageScroll.ActualWidth:0.#} viewport={PageScroll.ViewportWidth:0.#} grid={ContentGrid.ActualWidth:0.#} cards={CardsPanel.ActualWidth:0.#} dpi={dpi.DpiScaleX:0.##}{Environment.NewLine}");
            }
            catch
            {
            }
        }));
    }
}
