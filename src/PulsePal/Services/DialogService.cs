using System.Windows;
using PulsePal.Views;

namespace PulsePal.Services;

public sealed class DialogService : IDialogService
{
    public bool Confirm(string eyebrow, string title, string message, string confirmLabel, string cancelLabel)
    {
        var dialog = new PromptDialog(eyebrow, title, message, confirmLabel, cancelLabel)
        {
            Owner = FindOwner()
        };

        return dialog.ShowDialog() == true;
    }

    public void ShowMessage(string eyebrow, string title, string message, string closeLabel)
    {
        var dialog = new PromptDialog(eyebrow, title, message, closeLabel, cancelLabel: null)
        {
            Owner = FindOwner()
        };

        dialog.ShowDialog();
    }

    private static Window? FindOwner()
    {
        return Application.Current?.Windows
                   .OfType<MainWindow>()
                   .FirstOrDefault(window => window.IsVisible)
               ?? Application.Current?.MainWindow;
    }
}
