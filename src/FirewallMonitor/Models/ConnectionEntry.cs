namespace FirewallMonitor.Models;

/// <summary>One parsed line of pfirewall.log.</summary>
public sealed class ConnectionEntry
{
    public DateTime Timestamp { get; init; }
    public bool IsBlocked { get; init; }
    public string Protocol { get; init; } = "";
    public bool IsOutbound { get; init; }
    public bool DirectionKnown { get; init; }
    public string SrcIp { get; init; } = "";
    public string DstIp { get; init; } = "";
    public int SrcPort { get; init; }
    public int DstPort { get; init; }
    public int Size { get; init; }
    public string RawLine { get; init; } = "";

    // Filled by ProcessResolver (best effort, the log itself carries no process info).
    public int Pid { get; set; }
    public string ProcessName { get; set; } = "—";
    public string? ProcessPath { get; set; }

    public string LocalIp => IsOutbound || !DirectionKnown ? SrcIp : DstIp;
    public int LocalPort => IsOutbound || !DirectionKnown ? SrcPort : DstPort;
    public string RemoteIp => IsOutbound || !DirectionKnown ? DstIp : SrcIp;
    public int RemotePort => IsOutbound || !DirectionKnown ? DstPort : SrcPort;

    public string Local => Endpoint(LocalIp, LocalPort);
    public string Remote => Endpoint(RemoteIp, RemotePort);
    public string ActionText => IsBlocked ? "Blockiert" : "Zugelassen";
    public string DirectionText => !DirectionKnown ? "—" : IsOutbound ? "Ausgehend" : "Eingehend";
    public string PidText => Pid > 0 ? Pid.ToString() : "—";

    // Display helpers for the table and the details panel.
    public string DirectionShort => !DirectionKnown ? "—" : IsOutbound ? "Aus" : "Ein";
    public string RemotePortText => RemotePort > 0 ? RemotePort.ToString() : "—";
    public string LocalPortText => LocalPort > 0 ? LocalPort.ToString() : "—";
    public string ProcessPathText => ProcessPath ?? "—";
    public string SizeText => Size > 0 ? $"{Size} Byte" : "—";
    public string TimestampText => Timestamp.ToString("dd.MM.yyyy HH:mm:ss");

    public string Details =>
        $"Zeit:       {Timestamp:yyyy-MM-dd HH:mm:ss}\n" +
        $"Aktion:     {ActionText}\n" +
        $"Richtung:   {DirectionText}\n" +
        $"Protokoll:  {Protocol}\n" +
        $"Lokal:      {Local}\n" +
        $"Remote:     {Remote}\n" +
        $"Größe:      {Size} Byte\n" +
        $"Prozess:    {ProcessName} (PID {PidText})\n" +
        $"Programm:   {ProcessPath ?? "—"}\n" +
        $"Rohdaten:   {RawLine}";

    private static string Endpoint(string ip, int port)
    {
        if (port <= 0) return ip;
        return ip.Contains(':') ? $"[{ip}]:{port}" : $"{ip}:{port}";
    }
}
