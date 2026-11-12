using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using FirewallMonitor.Models;

namespace FirewallMonitor.Services;

public interface IFirewallService
{
    IReadOnlyList<ProfileStatus> GetProfiles();
    void SetProfileEnabled(int flag, bool enabled);
    IReadOnlyList<FirewallRuleInfo> GetRules();
    void AddRule(NewRuleRequest request);
    void SetRuleEnabled(string name, bool enabled);
    void DeleteRule(string name);
    void EnableLogging();
}

/// <summary>
/// Wraps the HNetCfg.FwPolicy2 COM API (late bound, so no interop assembly is needed).
/// A fresh policy object is created per call; calls are safe from any thread.
/// </summary>
public sealed class FirewallService : IFirewallService
{
    private const int Domain = 1, Private = 2, Public = 4;
    private const int AllProfiles = 0x7FFFFFFF;

    [ComImport, Guid("00020404-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumVariant
    {
        [PreserveSig]
        int Next(int celt, [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 0)] object[] rgVar, IntPtr fetched);
        [PreserveSig] int Skip(int celt);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumVariant ppEnum);
    }

    private static object CreatePolicy() =>
        Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2", true)!)!;

    private static object GetIndexed(object policy, string prop, int index) =>
        policy.GetType().InvokeMember(prop, BindingFlags.GetProperty, null, policy, new object[] { index })!;

    public IReadOnlyList<ProfileStatus> GetProfiles()
    {
        var policy = CreatePolicy();
        var list = new List<ProfileStatus>();
        foreach (var (name, flag) in new[] { ("Domäne", Domain), ("Privat", Private), ("Öffentlich", Public) })
            list.Add(new ProfileStatus(name, flag, (bool)GetIndexed(policy, "FirewallEnabled", flag)));
        return list;
    }

    public void SetProfileEnabled(int flag, bool enabled)
    {
        var policy = CreatePolicy();
        policy.GetType().InvokeMember("FirewallEnabled", BindingFlags.SetProperty, null, policy, new object[] { flag, enabled });
    }

    public IReadOnlyList<FirewallRuleInfo> GetRules()
    {
        dynamic policy = CreatePolicy();
        object rules = policy.Rules;
        var result = new List<FirewallRuleInfo>();

        foreach (var item in Enumerate(rules))
        {
            dynamic r = item;
            try
            {
                string? local = r.LocalPorts, remote = r.RemotePorts;
                int proto = r.Protocol;
                string? app = r.ApplicationName;
                result.Add(new FirewallRuleInfo
                {
                    Name = (string?)r.Name ?? "",
                    DirectionText = (int)r.Direction == 1 ? "Eingehend" : "Ausgehend",
                    ActionText = (int)r.Action == 1 ? "Zulassen" : "Blockieren",
                    Protocol = proto switch { 6 => "TCP", 17 => "UDP", 256 => "Beliebig", 1 => "ICMPv4", 58 => "ICMPv6", _ => proto.ToString() },
                    Ports = FormatPorts(local, remote),
                    Program = app ?? "",
                    Enabled = (bool)r.Enabled
                });
            }
            catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                // skip rules that cannot be read
            }
        }
        return result;
    }

    // Enumerates an INetFwRules collection via its DISPID_NEWENUM (-4) IEnumVARIANT.
    private static IEnumerable<object> Enumerate(object collection)
    {
        if (collection is IEnumerable plain)
        {
            foreach (var o in plain) yield return o;
            yield break;
        }

        var enumObj = collection.GetType().InvokeMember("[DispID=-4]", BindingFlags.GetProperty, null, collection, null);
        var en = (IEnumVariant)enumObj!;
        var buf = new object[1];
        while (en.Next(1, buf, IntPtr.Zero) == 0)
            yield return buf[0];
    }

    private static string FormatPorts(string? local, string? remote)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(local) && local != "*") parts.Add("lokal " + local);
        if (!string.IsNullOrEmpty(remote) && remote != "*") parts.Add("remote " + remote);
        return string.Join(", ", parts);
    }

    public void AddRule(NewRuleRequest req)
    {
        dynamic policy = CreatePolicy();
        dynamic rule = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule", true)!)!;

        rule.Name = req.Name;
        rule.Direction = req.IsInbound ? 1 : 2;
        rule.Action = req.Allow ? 1 : 0;
        rule.Enabled = true;
        rule.Profiles = req.Domain && req.Private && req.Public
            ? AllProfiles
            : (req.Domain ? Domain : 0) | (req.Private ? Private : 0) | (req.Public ? Public : 0);

        if (!string.IsNullOrWhiteSpace(req.ProgramPath)) rule.ApplicationName = req.ProgramPath;

        // Protocol must be set before any port.
        rule.Protocol = req.Protocol switch { "TCP" => 6, "UDP" => 17, _ => 256 };
        if (!string.IsNullOrWhiteSpace(req.Port))
        {
            if (req.IsInbound) rule.LocalPorts = req.Port; else rule.RemotePorts = req.Port;
        }
        if (!string.IsNullOrWhiteSpace(req.RemoteAddress)) rule.RemoteAddresses = req.RemoteAddress;

        policy.Rules.Add(rule);
    }

    public void SetRuleEnabled(string name, bool enabled)
    {
        dynamic policy = CreatePolicy();
        dynamic rule = policy.Rules.Item(name);
        rule.Enabled = enabled;
    }

    public void DeleteRule(string name)
    {
        dynamic policy = CreatePolicy();
        policy.Rules.Remove(name);
    }

    public void EnableLogging()
    {
        RunNetsh("advfirewall set allprofiles logging allowedconnections enable");
        RunNetsh("advfirewall set allprofiles logging droppedconnections enable");
        RunNetsh("advfirewall set allprofiles logging maxfilesize 16384");
    }

    private static void RunNetsh(string args)
    {
        using var p = Process.Start(new ProcessStartInfo("netsh", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"netsh {args}\n{output.Trim()}");
    }
}
