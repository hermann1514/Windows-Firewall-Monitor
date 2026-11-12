using FirewallMonitor.Models;

namespace FirewallMonitor.ViewModels;

public sealed class NewRuleViewModel : ObservableObject
{
    public static readonly string[] Protocols = { "Beliebig", "TCP", "UDP" };

    private string _name = "";
    private bool _isInbound;
    private bool _allow;
    private string _programPath = "";
    private string _protocol = "Beliebig";
    private string _port = "";
    private string _remoteAddress = "";

    public string Name { get => _name; set => Set(ref _name, value); }
    public bool IsInbound { get => _isInbound; set { Set(ref _isInbound, value); OnPropertyChanged(nameof(IsOutbound)); } }
    public bool IsOutbound { get => !_isInbound; set => IsInbound = !value; }
    public bool Allow { get => _allow; set { Set(ref _allow, value); OnPropertyChanged(nameof(Block)); } }
    public bool Block { get => !_allow; set => Allow = !value; }
    public string ProgramPath { get => _programPath; set => Set(ref _programPath, value); }
    public string Protocol { get => _protocol; set => Set(ref _protocol, value); }
    public string Port { get => _port; set => Set(ref _port, value); }
    public string RemoteAddress { get => _remoteAddress; set => Set(ref _remoteAddress, value); }
    public bool Domain { get; set; } = true;
    public bool Private { get; set; } = true;
    public bool Public { get; set; } = true;

    /// <summary>Returns an error message, or null when the input is valid.</summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) return "Bitte einen Namen angeben.";
        if (!Domain && !Private && !Public) return "Mindestens ein Profil auswählen.";
        if (string.IsNullOrWhiteSpace(ProgramPath) && string.IsNullOrWhiteSpace(Port) && string.IsNullOrWhiteSpace(RemoteAddress))
            return "Programmpfad, Port oder Remote-Adresse angeben.";
        if (!string.IsNullOrWhiteSpace(Port) && Protocol == "Beliebig")
            return "Für einen Port bitte TCP oder UDP wählen.";
        return null;
    }

    public NewRuleRequest ToRequest() => new()
    {
        Name = Name.Trim(),
        IsInbound = IsInbound,
        Allow = Allow,
        ProgramPath = ProgramPath.Trim(),
        Protocol = Protocol,
        Port = Port.Trim(),
        RemoteAddress = RemoteAddress.Trim(),
        Domain = Domain,
        Private = Private,
        Public = Public
    };
}
