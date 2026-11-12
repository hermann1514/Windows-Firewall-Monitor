using System.IO;
using System.Text;
using FirewallMonitor.Models;

namespace FirewallMonitor.Services;

/// <summary>
/// Live-reads pfirewall.log.
///
/// Why the text log and not the Event Log ("Windows Firewall With Advanced Security/Firewall"):
/// the event channel only carries rule changes; per-connection allow/drop events need the
/// "Filtering Platform" audit policy, are extremely verbose, and are harder to switch on
/// per profile. pfirewall.log has a stable column format, includes direction (path), is
/// enabled with a single netsh call and is trivially tailed. Weakness: no PID/process
/// (see <see cref="ProcessResolver"/>). FileSystemWatcher alone is unreliable for this
/// file (events are coalesced/delayed), so a 500 ms poll timer backs it up.
/// </summary>
public sealed class LogWatcherService : IDisposable
{
    public static readonly string DefaultLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "LogFiles", "Firewall", "pfirewall.log");

    private readonly string _path;
    private readonly PfLogParser _parser = new();
    private readonly ProcessResolver _resolver = new();
    private readonly object _gate = new();
    private FileSystemWatcher? _watcher;
    private Timer? _timer;
    private long _position;
    private string _leftover = "";

    /// <summary>Raised on a thread-pool thread with a batch of new entries (chronological order).</summary>
    public event Action<IReadOnlyList<ConnectionEntry>>? EntriesRead;
    public event Action<string>? Error;

    public LogWatcherService(string? path = null) => _path = path ?? DefaultLogPath;

    public string LogPath => _path;
    public bool LogExists => File.Exists(_path);

    public void Start()
    {
        // Live view only: start at the current end of the file.
        try { _position = File.Exists(_path) ? new FileInfo(_path).Length : 0; } catch { _position = 0; }

        var dir = System.IO.Path.GetDirectoryName(_path)!;
        if (Directory.Exists(dir))
        {
            _watcher = new FileSystemWatcher(dir, System.IO.Path.GetFileName(_path))
            {
                NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _watcher.Changed += (_, _) => ReadNew();
            _watcher.Created += (_, _) => ReadNew();
        }
        _timer = new Timer(_ => ReadNew(), null, 500, 500);
    }

    private void ReadNew()
    {
        if (!Monitor.TryEnter(_gate)) return; // another read is running
        try
        {
            if (!File.Exists(_path)) return;

            string text;
            using (var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (fs.Length < _position) { _position = 0; _leftover = ""; } // truncated / rotated
                if (fs.Length == _position) return;
                fs.Seek(_position, SeekOrigin.Begin);
                using var reader = new StreamReader(fs, Encoding.Latin1);
                text = _leftover + reader.ReadToEnd();
                _position = fs.Position;
            }

            var lines = text.Split('\n');
            _leftover = text.EndsWith('\n') ? "" : lines[^1];
            var complete = text.EndsWith('\n') ? lines.Length : lines.Length - 1;

            var batch = new List<ConnectionEntry>(complete);
            for (var i = 0; i < complete; i++)
            {
                var entry = _parser.Parse(lines[i].TrimEnd('\r'));
                if (entry != null) batch.Add(entry);
            }
            if (batch.Count == 0) return;

            _resolver.Refresh();
            foreach (var e in batch) _resolver.Resolve(e);
            EntriesRead?.Invoke(batch);
        }
        catch (IOException) { /* file briefly locked by the firewall service; retry on next tick */ }
        catch (Exception ex) { Error?.Invoke(ex.Message); }
        finally { Monitor.Exit(_gate); }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _watcher?.Dispose();
    }
}
