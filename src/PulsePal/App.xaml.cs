using System.Windows;
using PulsePal.ViewModels;
using PulsePal.Views;

namespace PulsePal;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow
        {
            DataContext = new MainViewModel()
        };

        MainWindow = window;
        window.Show();
    }
}
