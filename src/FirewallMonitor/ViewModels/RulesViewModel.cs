using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using FirewallMonitor.Models;
using FirewallMonitor.Services;
using FirewallMonitor.Views;

namespace FirewallMonitor.ViewModels;

public sealed class RulesViewModel : ObservableObject
{
    private readonly IFirewallService _fw;
    private readonly Action<string> _status;
    private string _filterText = "";
    private string _directionFilter = "Alle";
    private FirewallRuleInfo? _selected;

    public RulesViewModel(IFirewallService fw, Action<string> status)
    {
        _fw = fw;
        _status = status;
        View = CollectionViewSource.GetDefaultView(Rules);
        View.Filter = Match;

        RefreshCommand = new RelayCommand(_ => Run(RefreshAsync));
        NewCommand = new RelayCommand(_ => Run(() => CreateRuleAsync(new NewRuleViewModel())));
        EnableCommand = new RelayCommand(_ => Run(() => SetEnabledAsync(true)), _ => Selected is { Enabled: false });
        DisableCommand = new RelayCommand(_ => Run(() => SetEnabledAsync(false)), _ => Selected is { Enabled: true });
        DeleteCommand = new RelayCommand(_ => Run(DeleteAsync), _ => Selected != null);
    }

    public ObservableCollection<FirewallRuleInfo> Rules { get; } = new();
    public ICollectionView View { get; }

    public string FilterText
    {
        get => _filterText;
        set { if (Set(ref _filterText, value)) RefreshView(); }
    }

    public string DirectionFilter
    {
        get => _directionFilter;
        set { if (Set(ref _directionFilter, value)) RefreshView(); }
    }

    public FirewallRuleInfo? Selected { get => _selected; set => Set(ref _selected, value); }

    public string CountText
    {
        get
        {
            var visible = View.Cast<object>().Count();
            return visible == Rules.Count ? $"{Rules.Count:N0} Regeln" : $"{visible:N0} von {Rules.Count:N0} Regeln";
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EnableCommand { get; }
    public ICommand DisableCommand { get; }
    public ICommand DeleteCommand { get; }

    private bool Match(object o)
    {
        var r = (FirewallRuleInfo)o;
        if (DirectionFilter == "Eingehend" && !r.IsInbound) return false;
        if (DirectionFilter == "Ausgehend" && r.IsInbound) return false;

        var t = _filterText.Trim();
        if (t.Length == 0) return true;
        return r.Name.Contains(t, StringComparison.OrdinalIgnoreCase)
               || r.Program.Contains(t, StringComparison.OrdinalIgnoreCase)
               || r.Ports.Contains(t, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshView()
    {
        View.Refresh();
        OnPropertyChanged(nameof(CountText));
    }

    public async Task RefreshAsync()
    {
        _status("Lade Firewall-Regeln…");
        var rules = await Task.Run(_fw.GetRules);
        Rules.Clear();
        foreach (var r in rules.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)) Rules.Add(r);
        OnPropertyChanged(nameof(CountText));
        _status($"{Rules.Count:N0} Regeln geladen.");
    }

    /// <summary>Shows the rule dialog (prefilled by the caller) and creates the rule.</summary>
    public async Task CreateRuleAsync(NewRuleViewModel vm)
    {
        while (UiDialogs.ShowNewRule(vm))
        {
            try
            {
                var request = vm.ToRequest();
                await Task.Run(() => _fw.AddRule(request));
                _status($"Regel „{request.Name}“ angelegt.");
                await RefreshAsync();
                return;
            }
            catch (Exception ex)
            {
                UiDialogs.Error("Regel konnte nicht angelegt werden", ex.Message);
            }
        }
    }

    private async Task SetEnabledAsync(bool enabled)
    {
        var rule = Selected;
        if (rule == null) return;
        await Task.Run(() => _fw.SetRuleEnabled(rule.Name, enabled));
        rule.Enabled = enabled;
        _status($"Regel „{rule.Name}“ {(enabled ? "aktiviert" : "deaktiviert")}.");
    }

    private async Task DeleteAsync()
    {
        var rule = Selected;
        if (rule == null) return;
        if (!UiDialogs.Confirm("Regel löschen?", $"Die Regel „{rule.Name}“ wird dauerhaft aus der Windows Firewall entfernt.",
                "Löschen", danger: true))
            return;
        await Task.Run(() => _fw.DeleteRule(rule.Name));
        Rules.Remove(rule);
        OnPropertyChanged(nameof(CountText));
        _status($"Regel „{rule.Name}“ gelöscht.");
    }

    private static async void Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { UiDialogs.Error("Aktion fehlgeschlagen", ex.Message); }
    }
}
