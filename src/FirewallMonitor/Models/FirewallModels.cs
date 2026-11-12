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
        set => Set(ref _enabled, value);
    }
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
