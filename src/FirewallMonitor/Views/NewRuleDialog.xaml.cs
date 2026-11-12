using System.Windows;
using FirewallMonitor.ViewModels;
using Microsoft.Win32;

namespace FirewallMonitor.Views;

public partial class NewRuleDialog : Window
{
    private readonly NewRuleViewModel _vm;

    public NewRuleDialog(NewRuleViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        FirewallMonitor.Services.ThemeService.ApplyToWindow(this);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var error = _vm.Validate();
        ErrorText.Text = error ?? "";
        ErrorText.Visibility = error == null ? Visibility.Collapsed : Visibility.Visible;
        if (error == null) DialogResult = true;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Programme (*.exe)|*.exe|Alle Dateien|*.*" };
        if (dlg.ShowDialog(this) == true) _vm.ProgramPath = dlg.FileName;
    }
}
