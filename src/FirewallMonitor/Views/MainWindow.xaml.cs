using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirewallMonitor.Services;
using FirewallMonitor.ViewModels;

namespace FirewallMonitor.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(new FirewallService(), new LogWatcherService());
        DataContext = _vm;
        ThemeService.ApplyToWindow(this);

        // Newest entries are inserted at the top, so "auto-scroll" means: stay at the top.
        // While an entry is selected the user is inspecting it, so the list does not jump.
        _vm.BatchApplied += () =>
        {
            if (_vm.AutoScroll && !_vm.HasSelection && LiveGrid.Items.Count > 0)
                LiveGrid.ScrollIntoView(LiveGrid.Items[0]);
        };

        Loaded += (_, _) => _vm.Start();
        Closed += (_, _) => _vm.Dispose();
    }

    // Right click should select the row so the context menu acts on it.
    private void Row_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row)
        {
            row.IsSelected = true;
            row.Focus();
        }
    }
}
