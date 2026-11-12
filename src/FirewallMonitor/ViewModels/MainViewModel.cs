using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
    public static readonly string[] ActionFilters = { "Alle", "Zugelassen", "Blockiert" };

    private readonly IFirewallService _fw;
    private readonly LogWatcherService _watcher;
    private readonly ConcurrentQueue<ConnectionEntry> _queue = new();
    private readonly DispatcherTimer _batchTimer;
    private readonly DispatcherTimer _statsTimer;

    private bool _isPaused;
    private bool _autoScroll = true;
    private string _filterText = "";
    private string _filterColumn = "Alle";
    private string _actionFilter = "Alle";
    private string _status = "Bereit.";
    private ConnectionEntry? _selected;
    private bool _statsDirty;

    public MainViewModel(IFirewallService fw, LogWatcherService watcher)
    {
        _fw = fw;
        _watcher = watcher;

        View = CollectionViewSource.GetDefaultView(Entries);
        View.Filter = Match;
        Rules = new RulesViewModel(fw, s => StatusMessage = s);

        _watcher.EntriesRead += batch => { foreach (var e in batch) _queue.Enqueue(e); };
        _watcher.Error += msg => Application_Invoke(() => StatusMessage = "Log-Fehler: " + msg);

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

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (!Set(ref _isPaused, value)) return;
            OnPropertyChanged(nameof(PauseText));
            OnPropertyChanged(nameof(PendingText));
            if (!value) ApplyBatch(); // resume: flush everything collected meanwhile
        }
    }

    public string PauseText => IsPaused ? "▶ Fortsetzen" : "⏸ Pause";
    public string PendingText => IsPaused ? $"{_queue.Count} Einträge wartend" : "";
    public bool AutoScroll { get => _autoScroll; set => Set(ref _autoScroll, value); }

    public string FilterText { get => _filterText; set { if (Set(ref _filterText, value)) RefreshFilter(); } }
    public string FilterColumn { get => _filterColumn; set { if (Set(ref _filterColumn, value)) RefreshFilter(); } }
    public string ActionFilter { get => _actionFilter; set { if (Set(ref _actionFilter, value)) RefreshFilter(); } }

    public string StatusMessage { get => _status; set => Set(ref _status, value); }
    public string CountText => $"{View.Cast<object>().Count()} / {Entries.Count} Einträge";

    public ConnectionEntry? Selected
    {
        get => _selected;
        set { if (Set(ref _selected, value)) OnPropertyChanged(nameof(DetailsText)); }
    }

    public string DetailsText => Selected?.Details ?? "Eintrag auswählen, um alle Details zu sehen.";

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
                UiDialogs.Confirm("Die Firewall-Logdatei existiert noch nicht.\nLogging (erlaubt + blockiert) jetzt aktivieren?"))
                await EnableLoggingAsync();
            _watcher.Start();
        }
        catch (Exception ex)
        {
            UiDialogs.Error("Start fehlgeschlagen:\n" + ex.Message);
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
        if (!target && !UiDialogs.Confirm($"Firewall-Profil „{p.Name}“ wirklich ausschalten?")) return;
        await Task.Run(() => _fw.SetProfileEnabled(p.Flag, target));
        p.IsEnabled = target;
        StatusMessage = $"Profil {p.Name}: {p.StatusText}.";
    }

    private async Task EnableLoggingAsync()
    {
        await Task.Run(_fw.EnableLogging);
        StatusMessage = "Firewall-Logging aktiviert (erlaubt + blockiert).";
    }

    private void ApplyBatch()
    {
        OnPropertyChanged(nameof(PendingText));
        if (IsPaused || _queue.IsEmpty) return;

        var batch = new List<ConnectionEntry>();
        while (_queue.TryDequeue(out var e)) batch.Add(e);
        batch.Reverse(); // newest first

        var keep = Selected;
        Entries.PrependRange(batch, MaxEntries);
        if (keep != null) { _selected = keep; OnPropertyChanged(nameof(Selected)); } // Reset clears selection

        _statsDirty = true;
        OnPropertyChanged(nameof(CountText));
        BatchApplied?.Invoke();
    }

    private void RefreshFilter()
    {
        View.Refresh();
        _statsDirty = true;
        OnPropertyChanged(nameof(CountText));
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
            "IP" => C(e.SrcIp) || C(e.DstIp),
            "Port" => C(e.SrcPort.ToString()) || C(e.DstPort.ToString()),
            "Protokoll" => C(e.Protocol),
            "Aktion" => C(e.ActionText),
            _ => C(e.ProcessName) || C(e.SrcIp) || C(e.DstIp) || C(e.SrcPort.ToString()) ||
                 C(e.DstPort.ToString()) || C(e.Protocol) || C(e.ActionText) || C(e.DirectionText)
        };
    }

    private void UpdateStats()
    {
        _statsDirty = false;
        var visible = View.Cast<ConnectionEntry>().ToList();

        Fill(TopBlockedProcesses, visible.Where(e => e.IsBlocked)
            .GroupBy(e => e.ProcessName == "—" ? "(unbekannt)" : e.ProcessName));
        Fill(TopRemoteIps, visible.GroupBy(e => e.RemoteIp));
    }

    private static void Fill(ObservableCollection<CounterItem> target, IEnumerable<IGrouping<string, ConnectionEntry>> groups)
    {
        var top = groups.Select(g => (g.Key, Count: g.Count())).OrderByDescending(x => x.Count).Take(10).ToList();
        target.Clear();
        var max = top.Count > 0 ? top[0].Count : 1;
        foreach (var (key, count) in top) target.Add(new CounterItem(key, count, (double)count / max));
    }

    private void Export()
    {
        var path = UiDialogs.PickCsvPath();
        if (path == null) return;
        try
        {
            var rows = View.Cast<ConnectionEntry>().ToList();
            CsvExporter.Export(path, rows);
            StatusMessage = $"{rows.Count} Einträge exportiert: {path}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UiDialogs.Error("Export fehlgeschlagen:\n" + ex.Message);
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
                UiDialogs.Info("Für diesen Eintrag konnte kein Prozess ermittelt werden (kurzlebige oder blockierte Verbindung).");
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
        catch (Exception ex) { UiDialogs.Error(ex.Message); }
    }

    private static void Application_Invoke(Action a) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(a);

    public void Dispose()
    {
        _batchTimer.Stop();
        _statsTimer.Stop();
        _watcher.Dispose();
    }
}
