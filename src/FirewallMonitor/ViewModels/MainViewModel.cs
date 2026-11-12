using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using FirewallMonitor.Models;
using FirewallMonitor.Services;
using FirewallMonitor.Views;

namespace FirewallMonitor.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private const int MaxEntries = 20_000;

    public static readonly string[] FilterColumns = { "Alle", "Prozess", "IP", "Port", "Protokoll", "Aktion" };

    private readonly IFirewallService _fw;
    private readonly LogWatcherService _watcher;
    private readonly ConcurrentQueue<ConnectionEntry> _queue = new();
    private readonly DispatcherTimer _batchTimer;
    private readonly DispatcherTimer _statsTimer;

    private bool _isPaused;
    private bool _autoScroll = true;
    private bool _showInspector = true;
    private string _filterText = "";
    private string _filterColumn = "Alle";
    private string _actionFilter = "Alle";
    private string _status = "";
    private string _currentPage = "Live";
    private string _themeMode = ThemeService.Mode;
    private ConnectionEntry? _selected;
    private bool _statsDirty = true;
    private int _visibleCount, _blockedCount, _allowedCount;

    public MainViewModel(IFirewallService fw, LogWatcherService watcher)
    {
        _fw = fw;
        _watcher = watcher;

        View = CollectionViewSource.GetDefaultView(Entries);
        View.Filter = Match;
        Rules = new RulesViewModel(fw, s => StatusMessage = s);

        _watcher.EntriesRead += batch => { foreach (var e in batch) _queue.Enqueue(e); };
        _watcher.Error += msg => Application.Current?.Dispatcher.InvokeAsync(() => StatusMessage = "Log-Fehler: " + msg);

        // Batch updates: the UI drains the queue at a fixed rate instead of per-item updates.
        _batchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _batchTimer.Tick += (_, _) => ApplyBatch();
        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statsTimer.Tick += (_, _) => { if (_statsDirty) UpdateStats(); };

        RefreshCommand = new RelayCommand(_ => Run(RefreshAllAsync));
        TogglePauseCommand = new RelayCommand(_ => IsPaused = !IsPaused);
        ExportCommand = new RelayCommand(_ => Export());
        EnableLoggingCommand = new RelayCommand(_ => Run(EnableLoggingAsync));
        CreateProcessRuleCommand = new RelayCommand(_ => Run(() => QuickRuleAsync(false)), _ => Selected != null);
        CreateIpRuleCommand = new RelayCommand(_ => Run(() => QuickRuleAsync(true)), _ => Selected != null);
        ClearSelectionCommand = new RelayCommand(_ => Selected = null);
        CopyRawCommand = new RelayCommand(_ => CopyRaw(), _ => Selected != null);
        FilterProcessCommand = new RelayCommand(p => ApplyQuickFilter("Prozess", p as string));
        FilterIpCommand = new RelayCommand(p => ApplyQuickFilter("IP", p as string));
        ClearFilterCommand = new RelayCommand(_ => { FilterText = ""; FilterColumn = "Alle"; ActionFilter = "Alle"; });
    }

    public RangeObservableCollection<ConnectionEntry> Entries { get; } = new();
    public ICollectionView View { get; }
    public ObservableCollection<ProfileViewModel> Profiles { get; } = new();
    public ObservableCollection<CounterItem> TopBlockedProcesses { get; } = new();
    public ObservableCollection<CounterItem> TopRemoteIps { get; } = new();
    public RulesViewModel Rules { get; }

    public ICommand RefreshCommand { get; }
    public ICommand TogglePauseCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand EnableLoggingCommand { get; }
    public ICommand CreateProcessRuleCommand { get; }
    public ICommand CreateIpRuleCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand CopyRawCommand { get; }
    public ICommand FilterProcessCommand { get; }
    public ICommand FilterIpCommand { get; }
    public ICommand ClearFilterCommand { get; }

    public string LogPath => _watcher.LogPath;
    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";

    public string CurrentPage { get => _currentPage; set => Set(ref _currentPage, value); }

    public string ThemeMode
    {
        get => _themeMode;
        set { if (Set(ref _themeMode, value)) ThemeService.Apply(value); }
    }

    // ---------- Live state ----------

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (!Set(ref _isPaused, value)) return;
            OnPropertyChanged(nameof(PauseText));
            OnPropertyChanged(nameof(PauseGlyph));
            OnPropertyChanged(nameof(LiveText));
            if (!value) ApplyBatch(); // resume: flush everything collected meanwhile
        }
    }

    public string PauseText => IsPaused ? "Fortsetzen" : "Pause";
    public string PauseGlyph => IsPaused ? "" : "";
    public string LiveText => IsPaused ? "Pausiert" : "Live";
    public int PendingCount => _queue.Count;

    public bool AutoScroll { get => _autoScroll; set => Set(ref _autoScroll, value); }

    public bool ShowInspector
    {
        get => _showInspector;
        set { if (Set(ref _showInspector, value) && value) _statsDirty = true; }
    }

    // ---------- Filter ----------

    public string FilterText { get => _filterText; set { if (Set(ref _filterText, value)) RefreshFilter(); } }
    public string FilterColumn { get => _filterColumn; set { if (Set(ref _filterColumn, value)) RefreshFilter(); } }
    public string ActionFilter { get => _actionFilter; set { if (Set(ref _actionFilter, value)) RefreshFilter(); } }

    public bool IsFiltered => FilterText.Trim().Length > 0 || ActionFilter != "Alle";

    // ---------- Overview numbers (based on the current view) ----------

    public int VisibleCount => _visibleCount;
    public int BlockedCount => _blockedCount;
    public int AllowedCount => _allowedCount;

    public string CountText => _visibleCount == Entries.Count
        ? $"{Entries.Count:N0} Einträge"
        : $"{_visibleCount:N0} von {Entries.Count:N0} Einträgen";

    public string EmptyTitle => Entries.Count == 0 ? "Warte auf Verbindungen" : "Keine Treffer";

    public string EmptyText => Entries.Count == 0
        ? "Neue Einträge aus dem Firewall-Protokoll erscheinen hier automatisch."
        : "Für den aktuellen Filter gibt es keine Einträge.";

    public string StatusMessage { get => _status; set => Set(ref _status, value); }

    // ---------- Selection ----------

    public ConnectionEntry? Selected
    {
        get => _selected;
        set { if (Set(ref _selected, value)) OnPropertyChanged(nameof(HasSelection)); }
    }

    public bool HasSelection => _selected != null;

    /// <summary>Raised after a batch was added so the view can scroll to the newest entry.</summary>
    public event Action? BatchApplied;

    public async void Start()
    {
        _batchTimer.Start();
        _statsTimer.Start();
        try
        {
            await RefreshAllAsync();
            if (!_watcher.LogExists &&
                UiDialogs.Confirm("Firewall-Protokoll aktivieren?",
                    "Die Logdatei der Windows Firewall existiert noch nicht. Ohne Protokollierung können keine Verbindungen angezeigt werden.",
                    "Aktivieren"))
                await EnableLoggingAsync();
            _watcher.Start();
        }
        catch (Exception ex)
        {
            UiDialogs.Error("Start fehlgeschlagen", ex.Message);
        }
    }

    private async Task RefreshAllAsync()
    {
        var statuses = await Task.Run(_fw.GetProfiles);
        Profiles.Clear();
        foreach (var s in statuses) Profiles.Add(new ProfileViewModel(s, p => Run(() => ToggleProfileAsync(p))));
        await Rules.RefreshAsync();
    }

    private async Task ToggleProfileAsync(ProfileViewModel p)
    {
        var target = !p.IsEnabled;
        if (!target && !UiDialogs.Confirm($"Profil „{p.Name}“ ausschalten?",
                "Solange das Profil ausgeschaltet ist, filtert die Windows Firewall in Netzwerken dieses Typs keinen Datenverkehr.",
                "Ausschalten", danger: true))
            return;
        await Task.Run(() => _fw.SetProfileEnabled(p.Flag, target));
        p.IsEnabled = target;
        StatusMessage = $"Profil {p.Name}: {p.StatusText}.";
    }

    private async Task EnableLoggingAsync()
    {
        await Task.Run(_fw.EnableLogging);
        StatusMessage = "Firewall-Protokollierung aktiviert (zugelassene und blockierte Verbindungen).";
    }

    private void ApplyBatch()
    {
        OnPropertyChanged(nameof(PendingCount));
        if (IsPaused || _queue.IsEmpty) return;

        var batch = new List<ConnectionEntry>();
        while (_queue.TryDequeue(out var e)) batch.Add(e);
        batch.Reverse(); // newest first

        var keep = Selected;
        Entries.PrependRange(batch, MaxEntries);
        if (keep != null && !ReferenceEquals(_selected, keep))
        {
            // The Reset notification clears the grid selection; restore it.
            _selected = keep;
            OnPropertyChanged(nameof(Selected));
            OnPropertyChanged(nameof(HasSelection));
        }

        _statsDirty = true;
        BatchApplied?.Invoke();
    }

    private void RefreshFilter()
    {
        View.Refresh();
        OnPropertyChanged(nameof(IsFiltered));
        UpdateStats();
    }

    private void ApplyQuickFilter(string column, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        Selected = null;
        _filterColumn = column;
        OnPropertyChanged(nameof(FilterColumn));
        FilterText = value == "(unbekannt)" ? "—" : value;
    }

    private bool Match(object o)
    {
        var e = (ConnectionEntry)o;
        if (ActionFilter == "Blockiert" && !e.IsBlocked) return false;
        if (ActionFilter == "Zugelassen" && e.IsBlocked) return false;

        var t = FilterText.Trim();
        if (t.Length == 0) return true;

        bool C(string s) => s.Contains(t, StringComparison.OrdinalIgnoreCase);
        return FilterColumn switch
        {
            "Prozess" => C(e.ProcessName) || C(e.PidText),
            "IP" => C(e.SrcIp) || C(e.DstIp) || C(e.Local) || C(e.Remote),
            "Port" => C(e.SrcPort.ToString()) || C(e.DstPort.ToString()) || C(e.Local) || C(e.Remote),
            "Protokoll" => C(e.Protocol),
            "Aktion" => C(e.ActionText),
            _ => C(e.ProcessName) || C(e.SrcIp) || C(e.DstIp) || C(e.Local) || C(e.Remote) || C(e.SrcPort.ToString()) ||
                 C(e.DstPort.ToString()) || C(e.Protocol) || C(e.ActionText) || C(e.DirectionText)
        };
    }

    private void UpdateStats()
    {
        _statsDirty = false;

        var visible = View.Cast<ConnectionEntry>().ToList();
        _visibleCount = visible.Count;
        _blockedCount = visible.Count(e => e.IsBlocked);
        _allowedCount = _visibleCount - _blockedCount;
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(BlockedCount));
        OnPropertyChanged(nameof(AllowedCount));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyText));

        if (!ShowInspector) return;
        Fill(TopBlockedProcesses, visible.Where(e => e.IsBlocked)
            .GroupBy(e => e.ProcessName == "—" ? "(unbekannt)" : e.ProcessName));
        Fill(TopRemoteIps, visible.GroupBy(e => e.RemoteIp));
    }

    private static void Fill(ObservableCollection<CounterItem> target, IEnumerable<IGrouping<string, ConnectionEntry>> groups)
    {
        var top = groups.Select(g => (g.Key, Count: g.Count())).OrderByDescending(x => x.Count).Take(8).ToList();
        target.Clear();
        var max = top.Count > 0 ? top[0].Count : 1;
        foreach (var (key, count) in top) target.Add(new CounterItem(key, count, (double)count / max));
    }

    private void CopyRaw()
    {
        if (Selected == null) return;
        try
        {
            Clipboard.SetText(Selected.RawLine);
            StatusMessage = "Rohzeile in die Zwischenablage kopiert.";
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            StatusMessage = "Zwischenablage ist gerade belegt – bitte erneut versuchen.";
        }
    }

    private void Export()
    {
        var path = UiDialogs.PickCsvPath();
        if (path == null) return;
        try
        {
            var rows = View.Cast<ConnectionEntry>().ToList();
            CsvExporter.Export(path, rows);
            StatusMessage = $"{rows.Count:N0} Einträge exportiert: {path}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UiDialogs.Error("Export fehlgeschlagen", ex.Message);
        }
    }

    /// <summary>Quick action from the live list: prefilled rule dialog for the process or the remote IP.</summary>
    private async Task QuickRuleAsync(bool byIp)
    {
        var e = Selected;
        if (e == null) return;

        var vm = new NewRuleViewModel
        {
            IsInbound = !e.IsOutbound && e.DirectionKnown,
            Allow = false
        };

        if (byIp)
        {
            vm.Name = $"Block {e.RemoteIp}";
            vm.RemoteAddress = e.RemoteIp;
        }
        else
        {
            if (string.IsNullOrEmpty(e.ProcessPath))
            {
                UiDialogs.Info("Kein Prozess bekannt",
                    "Für diese Verbindung konnte kein Prozess ermittelt werden (kurzlebige oder blockierte Verbindung). Lege stattdessen eine Regel für die IP-Adresse an.");
                return;
            }
            vm.Name = $"Block {e.ProcessName}";
            vm.ProgramPath = e.ProcessPath;
        }
        await Rules.CreateRuleAsync(vm);
    }

    private static async void Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { UiDialogs.Error("Aktion fehlgeschlagen", ex.Message); }
    }

    public void Dispose()
    {
        _batchTimer.Stop();
        _statsTimer.Stop();
        _watcher.Dispose();
    }
}
