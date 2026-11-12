using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using FirewallMonitor.Views;

namespace FirewallMonitor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        FirewallMonitor.Services.ThemeService.Load();

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Unerwarteter Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        if (!IsAdministrator())
        {
            var answer = MessageBox.Show(
                "Firewall Monitor benötigt Administratorrechte (Firewall lesen/ändern, Logging aktivieren).\n\nNeu als Administrator starten?",
                "Administratorrechte erforderlich", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" });
                }
                catch (Win32Exception)
                {
                    // UAC prompt declined.
                }
            }
            Shutdown();
            return;
        }

        new MainWindow().Show();
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
