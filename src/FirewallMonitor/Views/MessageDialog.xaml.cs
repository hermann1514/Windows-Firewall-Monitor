using System.Windows;
using FirewallMonitor.Services;

namespace FirewallMonitor.Views;

public enum MessageKind { Info, Error, Confirm }

/// <summary>Themed replacement for MessageBox (info, error, confirmation).</summary>
public partial class MessageDialog : Window
{
    private MessageDialog(MessageKind kind, string title, string message, string confirmText, bool danger)
    {
        InitializeComponent();
        ThemeService.ApplyToWindow(this);

        TitleText.Text = title;
        MessageText.Text = message;
        MessageText.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
        OkButton.Content = confirmText;

        switch (kind)
        {
            case MessageKind.Error:
                IconText.Text = "";
                IconText.SetResourceReference(ForegroundProperty, "Danger");
                CancelButton.Visibility = Visibility.Collapsed;
                break;
            case MessageKind.Info:
                IconText.Text = "";
                CancelButton.Visibility = Visibility.Collapsed;
                break;
            case MessageKind.Confirm:
                IconText.Text = danger ? "" : "";
                if (danger)
                {
                    IconText.SetResourceReference(ForegroundProperty, "Danger");
                    OkButton.Style = (Style)FindResource("DangerButton");
                }
                break;
        }
    }

    public static bool Show(Window? owner, MessageKind kind, string title, string message,
        string confirmText = "OK", bool danger = false)
    {
        var dlg = new MessageDialog(kind, title, message, confirmText, danger);
        if (owner is { IsLoaded: true, IsVisible: true })
            dlg.Owner = owner;
        else
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return dlg.ShowDialog() == true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
