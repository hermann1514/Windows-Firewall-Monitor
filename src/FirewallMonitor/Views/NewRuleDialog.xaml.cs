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
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var error = _vm.Validate();
        if (error != null)
        {
            MessageBox.Show(this, error, "Eingabe prüfen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Programme (*.exe)|*.exe|Alle Dateien|*.*" };
        if (dlg.ShowDialog(this) == true) _vm.ProgramPath = dlg.FileName;
    }
}
