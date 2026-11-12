using System.Globalization;
using FirewallMonitor.Models;

namespace FirewallMonitor.Services;

/// <summary>Parses pfirewall.log lines (space separated, columns announced by "#Fields:").</summary>
public sealed class PfLogParser
{
    private static readonly string[] DefaultFields =
    {
        "date", "time", "action", "protocol", "src-ip", "dst-ip", "src-port", "dst-port",
        "size", "tcpflags", "tcpsyn", "tcpack", "tcpwin", "icmptype", "icmpcode", "info", "path"
    };

    private Dictionary<string, int> _index = BuildIndex(DefaultFields);

    private static Dictionary<string, int> BuildIndex(IEnumerable<string> fields)
    {
        var d = new Dictionary<string, int>();
        var i = 0;
        foreach (var f in fields) d[f] = i++;
        return d;
    }

    public ConnectionEntry? Parse(string line)
    {
        if (line.Length == 0) return null;

        if (line[0] == '#')
        {
            if (line.StartsWith("#Fields:", StringComparison.Ordinal))
                _index = BuildIndex(line[8..].Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return null;
        }

        var p = line.Split(' ');
        string Get(string name) => _index.TryGetValue(name, out var i) && i < p.Length ? p[i] : "-";

        if (!DateTime.TryParse(Get("date") + " " + Get("time"), CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var ts))
            return null;

        var path = Get("path");
        var known = path is "SEND" or "RECEIVE";

        return new ConnectionEntry
        {
            Timestamp = ts,
            IsBlocked = Get("action").StartsWith("DROP", StringComparison.OrdinalIgnoreCase),
            Protocol = Get("protocol").ToUpperInvariant(),
            DirectionKnown = known,
            IsOutbound = path == "SEND",
            SrcIp = Get("src-ip"),
            DstIp = Get("dst-ip"),
            SrcPort = ParseInt(Get("src-port")),
            DstPort = ParseInt(Get("dst-port")),
            Size = ParseInt(Get("size")),
            RawLine = line
        };
    }

    private static int ParseInt(string s) => int.TryParse(s, out var v) ? v : 0;
}
