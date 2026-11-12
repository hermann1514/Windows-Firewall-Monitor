using System.Windows;
using FirewallMonitor.ViewModels;
using Microsoft.Win32;

namespace FirewallMonitor.Views;

/// <summary>Thin wrapper so view models do not touch WPF dialogs directly.</summary>
public static class UiDialogs
{
    public static void Error(string message) =>
        MessageBox.Show(message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);

    public static void Info(string message) =>
        MessageBox.Show(message, "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);

    public static bool Confirm(string message) =>
        MessageBox.Show(message, "Bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

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
