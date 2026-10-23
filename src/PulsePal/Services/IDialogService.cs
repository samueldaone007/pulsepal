namespace PulsePal.Services;

public interface IDialogService
{
    bool Confirm(string eyebrow, string title, string message, string confirmLabel, string cancelLabel);

    void ShowMessage(string eyebrow, string title, string message, string closeLabel);
}
