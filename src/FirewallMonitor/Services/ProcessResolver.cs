using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FirewallMonitor.Models;

namespace FirewallMonitor.Services;

/// <summary>
/// pfirewall.log has no process information. Best effort: map local port -> PID via the
/// IP Helper socket tables, then PID -> name/path. Short-lived or blocked inbound
/// connections have no socket and stay unresolved ("—").
/// </summary>
public sealed class ProcessResolver
{
    private const int AfInet = 2, AfInet6 = 23;
    private const int TcpTableOwnerPidAll = 5, UdpTableOwnerPid = 1;

    private readonly Dictionary<int, int> _tcp = new();
    private readonly Dictionary<int, int> _udp = new();
    private readonly Dictionary<int, (string Name, string? Path)> _procCache = new();

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int af, int cls, uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(IntPtr table, ref int size, bool order, int af, int cls, uint reserved);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder sb, ref int size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr h);

    /// <summary>Rebuild the port->PID snapshot. Call once per batch.</summary>
    public void Refresh()
    {
        _tcp.Clear();
        _udp.Clear();
        if (_procCache.Count > 2000) _procCache.Clear();
        ReadTable(true, AfInet, 24, 4, 20, _tcp);
        ReadTable(true, AfInet6, 56, 20, 52, _tcp);
        ReadTable(false, AfInet, 12, 4, 8, _udp);
        ReadTable(false, AfInet6, 28, 20, 24, _udp);
    }

    public void Resolve(ConnectionEntry e)
    {
        var port = e.LocalPort;
        if (port <= 0) return;
        var map = e.Protocol == "TCP" ? _tcp : e.Protocol == "UDP" ? _udp : null;
        if (map == null || !map.TryGetValue(port, out var pid) || pid <= 0) return;

        if (!_procCache.TryGetValue(pid, out var info))
        {
            info = LookupProcess(pid);
            _procCache[pid] = info;
        }
        e.Pid = pid;
        e.ProcessName = info.Name;
        e.ProcessPath = info.Path;
    }

    private static (string Name, string? Path) LookupProcess(int pid)
    {
        string name;
        try { name = Process.GetProcessById(pid).ProcessName; }
        catch { return ("—", null); }

        string? path = null;
        var h = OpenProcess(0x1000 /* QUERY_LIMITED_INFORMATION */, false, pid);
        if (h != IntPtr.Zero)
        {
            var sb = new StringBuilder(1024);
            var size = sb.Capacity;
            if (QueryFullProcessImageName(h, 0, sb, ref size)) path = sb.ToString();
            CloseHandle(h);
        }
        return (name, path);
    }

    private static void ReadTable(bool tcp, int af, int rowSize, int portOff, int pidOff, Dictionary<int, int> target)
    {
        var size = 0;
        var buf = IntPtr.Zero;
        try
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var rc = tcp
                    ? GetExtendedTcpTable(buf, ref size, false, af, TcpTableOwnerPidAll, 0)
                    : GetExtendedUdpTable(buf, ref size, false, af, UdpTableOwnerPid, 0);
                if (rc == 0) break;
                if (rc != 122) return; // anything but ERROR_INSUFFICIENT_BUFFER
                if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
                buf = Marshal.AllocHGlobal(size);
            }
            if (buf == IntPtr.Zero) return;

            var count = Marshal.ReadInt32(buf);
            for (var i = 0; i < count; i++)
            {
                var row = buf + 4 + i * rowSize;
                var raw = Marshal.ReadInt32(row + portOff);
                var port = ((raw & 0xFF) << 8) | ((raw >> 8) & 0xFF); // network byte order
                target[port] = Marshal.ReadInt32(row + pidOff);
            }
        }
        finally
        {
            if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
        }
    }
}
