using System.Windows.Input;
using FirewallMonitor.Models;

namespace FirewallMonitor.ViewModels;

public sealed class ProfileViewModel : ObservableObject
{
    private bool _isEnabled;

    public ProfileViewModel(ProfileStatus status, Action<ProfileViewModel> toggle)
    {
        Name = status.Name;
        Flag = status.Flag;
        _isEnabled = status.Enabled;
        ToggleCommand = new RelayCommand(_ => toggle(this));
    }

    public string Name { get; }
    public int Flag { get; }
    public ICommand ToggleCommand { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!Set(ref _isEnabled, value)) return;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(ToolTip));
        }
    }

    public string StatusText => IsEnabled ? "Aktiv" : "Deaktiviert";

    public string ToolTip => IsEnabled
        ? $"Profil „{Name}“ ist aktiv. Klicken zum Ausschalten."
        : $"Profil „{Name}“ ist ausgeschaltet. Klicken zum Einschalten.";
}
