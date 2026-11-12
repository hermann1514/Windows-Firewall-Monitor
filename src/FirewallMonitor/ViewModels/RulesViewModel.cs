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
    private FirewallRuleInfo? _selected;

    public RulesViewModel(IFirewallService fw, Action<string> status)
    {
        _fw = fw;
        _status = status;
        View = CollectionViewSource.GetDefaultView(Rules);
        View.Filter = o =>
        {
            if (string.IsNullOrWhiteSpace(_filterText)) return true;
            var r = (FirewallRuleInfo)o;
            return r.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                   || r.Program.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                   || r.Ports.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
        };

        RefreshCommand = new RelayCommand(_ => Run(RefreshAsync));
        NewCommand = new RelayCommand(_ => Run(() => CreateRuleAsync(new NewRuleViewModel())));
        EnableCommand = new RelayCommand(_ => Run(() => SetEnabledAsync(true)), _ => Selected != null);
        DisableCommand = new RelayCommand(_ => Run(() => SetEnabledAsync(false)), _ => Selected != null);
        DeleteCommand = new RelayCommand(_ => Run(DeleteAsync), _ => Selected != null);
    }

    public ObservableCollection<FirewallRuleInfo> Rules { get; } = new();
    public ICollectionView View { get; }

    public string FilterText
    {
        get => _filterText;
        set { if (Set(ref _filterText, value)) View.Refresh(); }
    }

    public FirewallRuleInfo? Selected { get => _selected; set => Set(ref _selected, value); }

    public ICommand RefreshCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EnableCommand { get; }
    public ICommand DisableCommand { get; }
    public ICommand DeleteCommand { get; }

    public async Task RefreshAsync()
    {
        _status("Lade Firewall-Regeln…");
        var rules = await Task.Run(_fw.GetRules);
        Rules.Clear();
        foreach (var r in rules.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)) Rules.Add(r);
        _status($"{Rules.Count} Regeln geladen.");
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
                UiDialogs.Error("Regel konnte nicht angelegt werden:\n" + ex.Message);
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
        if (rule == null || !UiDialogs.Confirm($"Regel „{rule.Name}“ wirklich löschen?")) return;
        await Task.Run(() => _fw.DeleteRule(rule.Name));
        Rules.Remove(rule);
        _status($"Regel „{rule.Name}“ gelöscht.");
    }

    private static async void Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { UiDialogs.Error(ex.Message); }
    }
}
