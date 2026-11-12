using System.Text;
using FirewallMonitor.Models;

namespace FirewallMonitor.Services;

public static class CsvExporter
{
    public static void Export(string path, IEnumerable<ConnectionEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Zeit;Prozess;PID;Richtung;Protokoll;Lokal-IP;Lokal-Port;Remote-IP;Remote-Port;Aktion;Groesse;Rohdaten");
        foreach (var e in entries)
        {
            sb.AppendJoin(';',
                e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), Q(e.ProcessName), e.PidText, e.DirectionText, e.Protocol,
                Q(e.LocalIp), e.LocalPort, Q(e.RemoteIp), e.RemotePort, e.ActionText, e.Size, Q(e.RawLine));
            sb.AppendLine();
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string Q(string s) => s.IndexOfAny(new[] { ';', '"', '\n' }) >= 0
        ? "\"" + s.Replace("\"", "\"\"") + "\""
        : s;
}
