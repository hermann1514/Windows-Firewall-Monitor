using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace FirewallMonitor.Services;

/// <summary>Switches the Colors.*.xaml dictionary at runtime and persists the choice. Modes: Dark, Light, System.</summary>
public static class ThemeService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FirewallMonitor", "settings.json");

    private sealed class Settings { public string Theme { get; set; } = "System"; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static string Mode { get; private set; } = "System";
    public static event Action? Changed;

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
                if (s?.Theme is "Dark" or "Light" or "System") Mode = s.Theme;
            }
        }
        catch { /* corrupt settings: keep default */ }
        Apply(Mode, save: false);
    }

    public static void Apply(string mode, bool save = true)
    {
        Mode = mode;
        var name = IsDark(mode) ? "Dark" : "Light";
        var dicts = Application.Current.Resources.MergedDictionaries;
        var fresh = new ResourceDictionary { Source = new Uri($"pack://application:,,,/Themes/Colors.{name}.xaml") };

        var old = dicts.FirstOrDefault(d => d.Source?.OriginalString.Contains("/Colors.") == true);
        if (old != null) dicts[dicts.IndexOf(old)] = fresh; else dicts.Insert(0, fresh);

        if (save)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new Settings { Theme = mode }));
            }
            catch { /* settings are a convenience only */ }
        }
        Changed?.Invoke();
    }

    private static bool IsDark(string mode)
    {
        if (mode == "Dark") return true;
        if (mode == "Light") return false;
        return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1) is int v && v == 0;
    }

    /// <summary>Makes the native title bar follow the theme (Windows 10 1809+ / 11).</summary>
    public static void ApplyToWindow(Window window)
    {
        void Update()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            var dark = IsDark(Mode) ? 1 : 0;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));          // immersive dark mode
            SetColor(hwnd, 35, "WindowBg");                                  // caption color (Win11)
            SetColor(hwnd, 36, "TextPrimary");                               // caption text color (Win11)
        }

        window.SourceInitialized += (_, _) => Update();
        Changed += Update;
        window.Closed += (_, _) => Changed -= Update;
    }

    private static void SetColor(IntPtr hwnd, int attr, string brushKey)
    {
        if (Application.Current.Resources[brushKey] is not SolidColorBrush b) return;
        var c = b.Color;
        var colorRef = c.R | (c.G << 8) | (c.B << 16);
        DwmSetWindowAttribute(hwnd, attr, ref colorRef, sizeof(int));
    }
}
