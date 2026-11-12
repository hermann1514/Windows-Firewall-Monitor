using FirewallMonitor.ViewModels;

namespace FirewallMonitor.Models;

public sealed record ProfileStatus(string Name, int Flag, bool Enabled);

public sealed class FirewallRuleInfo : ObservableObject
{
    private bool _enabled;

    public string Name { get; init; } = "";
    public string DirectionText { get; init; } = "";
    public string ActionText { get; init; } = "";
    public string Protocol { get; init; } = "";
    public string Ports { get; init; } = "";
    public string Program { get; init; } = "";

    public bool Enabled
    {
        get => _enabled;
        set { if (Set(ref _enabled, value)) OnPropertyChanged(nameof(EnabledText)); }
    }

    public string EnabledText => Enabled ? "Aktiv" : "Inaktiv";
    public bool IsInbound => DirectionText == "Eingehend";
    public bool IsBlock => ActionText == "Blockieren";
    public string DirectionShort => IsInbound ? "Ein" : "Aus";
    public string ProgramName => string.IsNullOrEmpty(Program) ? "—" : Path.GetFileName(Program);
    public string PortsText => string.IsNullOrEmpty(Ports) ? "—" : Ports;
}

public sealed class NewRuleRequest
{
    public string Name { get; init; } = "";
    public bool IsInbound { get; init; }
    public bool Allow { get; init; }
    public string ProgramPath { get; init; } = "";
    public string Protocol { get; init; } = "Beliebig"; // Beliebig | TCP | UDP
    public string Port { get; init; } = "";
    public string RemoteAddress { get; init; } = "";
    public bool Domain { get; init; }
    public bool Private { get; init; }
    public bool Public { get; init; }
}

public sealed record CounterItem(string Label, int Count, double Fraction);
