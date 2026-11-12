using System.Windows;
using FirewallMonitor.ViewModels;
using Microsoft.Win32;

namespace FirewallMonitor.Views;

/// <summary>Thin wrapper so view models do not touch WPF dialogs directly.</summary>
public static class UiDialogs
{
    private static Window? Owner =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;

    public static void Error(string title, string message) =>
        MessageDialog.Show(Owner, MessageKind.Error, title, message);

    public static void Info(string title, string message) =>
        MessageDialog.Show(Owner, MessageKind.Info, title, message);

    public static bool Confirm(string title, string message, string confirmText, bool danger = false) =>
        MessageDialog.Show(Owner, MessageKind.Confirm, title, message, confirmText, danger);

    public static string? PickCsvPath()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"firewall-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public static bool ShowNewRule(NewRuleViewModel vm)
    {
        var dlg = new NewRuleDialog(vm) { Owner = Application.Current.MainWindow };
        return dlg.ShowDialog() == true;
    }
}
