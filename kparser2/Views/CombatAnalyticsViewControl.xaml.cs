using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using kparser2.Abstractions;
using kparser2.Services;

namespace kparser2.Views;

public sealed class CombatAnalyticsViewControl : UserControl
{
    private readonly IDisposable _subscription;
    private bool _populating, _disposed;
    public CombatAnalyticsViewControl(IAnalyticsSession session, string queryId, bool combat)
    {
        var preferences = ViewSettingsService.Shared.Report(queryId);
        var dock = new DockPanel();
        var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(toolbar, Dock.Top);
        var modes = Modes(queryId);
        var modeOptions = modes.Select(m => new ModeOption(m, ModeLabel(m))).ToArray();
        var mode = new ComboBox { ItemsSource = modeOptions, DisplayMemberPath = nameof(ModeOption.Label), MinWidth = 125, Margin = new Thickness(4) };
        mode.SelectedItem = Enum.TryParse<ReportMode>(preferences.Category, out var savedMode) && modes.Contains(savedMode)
            ? modeOptions.First(o => o.Value == savedMode)
            : modeOptions[0];
        var players = new ComboBox { MinWidth = 140, Margin = new Thickness(4) };
        var mobs = new ComboBox { MinWidth = 180, Margin = new Thickness(4) };
        var grouped = new CheckBox { Content = "Group enemies", IsChecked = preferences.GroupMobs, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        var zeroXp = new CheckBox { Content = "Exclude 0 XP", IsChecked = preferences.ExcludeZeroXp, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        var detail = new CheckBox { Content = "Show details", IsChecked = preferences.ShowDetails, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        var crystals = new CheckBox { Content = "Exclude crystals", IsChecked = preferences.ExcludeCrystals, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        var baseAttacks = new ComboBox { ItemsSource = new[] { 1, 2 }, SelectedItem = preferences.BaseAttacks, Width = 60, Margin = new Thickness(4) };
        var fightButton = new Button { Content = "Select fights…", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(4) };
        var reset = new Button { Content = "Reset filters", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(4) };
        var queryView = new QueryAnalyticsViewControl(session, queryId);
        foreach (var (control, id) in new (DependencyObject, string)[]
        {
            (mode, "report-mode"), (players, "player-filter"), (mobs, "enemy-filter"),
            (grouped, "group-enemies"), (zeroXp, "exclude-zero-xp"), (detail, "show-details"),
            (crystals, "exclude-crystals"), (baseAttacks, "base-attacks"),
            (fightButton, "select-fights"), (reset, "reset-filters")
        }) AutomationProperties.SetAutomationId(control, id);
        IReadOnlyList<int>? selectedFights = preferences.BattleIds?.Distinct().ToArray();
        if (selectedFights is { } savedFights) fightButton.Content = $"{savedFights.Count} fights selected";
        void Add(string label, UIElement control)
        {
            toolbar.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4) });
            toolbar.Children.Add(control);
        }
        if (modes.Length > 1) Add("Report", mode);
        // These reports have no player attribution in the snapshot; showing a
        // selector here would imply a filter that the report cannot honor.
        if (queryId is not ("fights" or "players" or "experience")) Add("Player", players);
        if (combat) { Add("Enemy", mobs); toolbar.Children.Add(grouped); toolbar.Children.Add(zeroXp); toolbar.Children.Add(fightButton); }
        if (queryId == "items") toolbar.Children.Add(detail);
        if (queryId == "loot") toolbar.Children.Add(crystals);
        if (queryId == "extra-attacks") Add("Base attacks", baseAttacks);
        toolbar.Children.Add(reset);
        void Apply()
        {
            if (_populating || _disposed) return;
            var selectedMode = mode.SelectedItem is ModeOption selected ? selected.Value : modes[0];
            preferences.Category = selectedMode.ToString();
            preferences.Player = players.SelectedItem as string is { } p && p != "(All)" ? p : null;
            preferences.Mob = grouped.IsChecked == true && mobs.SelectedItem is EnemyOption { Name: not null } m ? m.Name : null;
            preferences.GroupMobs = grouped.IsChecked == true;
            preferences.ExcludeZeroXp = zeroXp.IsChecked == true;
            preferences.ShowDetails = detail.IsChecked == true;
            preferences.ExcludeCrystals = crystals.IsChecked == true;
            preferences.BaseAttacks = baseAttacks.SelectedItem is int count ? count : 1;
            preferences.BattleIds = selectedFights?.ToList();
            ViewSettingsService.Shared.Save();
            queryView.SetRequest(new AnalyticsReportRequest
            {
                QueryId = queryId, Mode = selectedMode, BattleIds = selectedFights,
                ShowDetails = preferences.ShowDetails, ExcludeCrystals = preferences.ExcludeCrystals, BaseAttacks = preferences.BaseAttacks,
                Filter = new MobFilterDto
                {
                    GroupMobs = preferences.GroupMobs, ExcludeZeroXp = preferences.ExcludeZeroXp,
                    SelectedPlayerName = preferences.Player, SelectedMobName = preferences.Mob,
                    SelectedBattleId = grouped.IsChecked != true && mobs.SelectedItem is EnemyOption option ? option.Id : null
                }
            });
        }
        void Populate(AnalyticsSnapshotDto snapshot)
        {
            if (_disposed) return;
            _populating = true;
            var player = players.SelectedItem as string ?? preferences.Player ?? "(All)";
            var playerOptions = new[] { "(All)" }.Concat(snapshot.Combatants.Where(c => c.Kind is "Player" or "Pet" or "Fellow").Select(c => c.Name)).Append(player).Distinct().OrderBy(n => n).ToArray();
            if (players.ItemsSource is not string[] previous || !previous.SequenceEqual(playerOptions)) players.ItemsSource = playerOptions;
            players.SelectedItem = player;
            var current = mobs.SelectedItem as EnemyOption;
            var options = new List<EnemyOption> { new(null, null, "(All)") };
            if (grouped.IsChecked == true)
            {
                options.AddRange(snapshot.Battles.Select(b => b.EnemyName).Distinct().OrderBy(n => n).Select(n => new EnemyOption(null, n, n)));
                if (preferences.Mob is string saved && !options.Any(o => o.Name == saved)) options.Add(new(null, saved, saved));
            }
            else options.AddRange(snapshot.Battles.Select(b => new EnemyOption(b.Id, b.EnemyName, $"#{b.Id} {b.EnemyName}")));
            if (mobs.ItemsSource is not List<EnemyOption> old || !old.SequenceEqual(options)) mobs.ItemsSource = options;
            mobs.SelectedItem = options.FirstOrDefault(o => grouped.IsChecked == true ? o.Name == (current?.Name ?? preferences.Mob) : o.Id == current?.Id) ?? options[0];
            _populating = false;
        }
        mode.SelectionChanged += (_, _) => Apply(); players.SelectionChanged += (_, _) => Apply(); mobs.SelectionChanged += (_, _) => Apply();
        grouped.Click += (_, _) => { Populate(session.GetSnapshot()); Apply(); };
        zeroXp.Click += (_, _) => Apply(); detail.Click += (_, _) => Apply(); crystals.Click += (_, _) => Apply(); baseAttacks.SelectionChanged += (_, _) => Apply();
        fightButton.Click += (_, _) =>
        {
            var list = new ListBox { SelectionMode = SelectionMode.Multiple, DisplayMemberPath = nameof(EnemyOption.Label), MinHeight = 240 };
            AutomationProperties.SetAutomationId(list, "fight-selection");
            var options = session.GetSnapshot().Battles.Select(b => new EnemyOption(b.Id, b.EnemyName, $"#{b.Id} {b.EnemyName} — {b.ExperiencePoints} XP")).ToArray();
            list.ItemsSource = options;
            foreach (var option in options.Where(o => selectedFights is null || selectedFights.Contains(o.Id!.Value))) list.SelectedItems.Add(option);
            var panel = new DockPanel { Margin = new Thickness(12) };
            var apply = new Button { Content = "Apply selected fights", Padding = new Thickness(10), Margin = new Thickness(0, 8, 0, 0) };
            AutomationProperties.SetAutomationId(apply, "apply-fights");
            DockPanel.SetDock(apply, Dock.Bottom); panel.Children.Add(apply); panel.Children.Add(list);
            var qa = Environment.GetEnvironmentVariable("KPARSER2_UI_QA") == "1";
            var window = new Window
            {
                Owner = Window.GetWindow(this),
                Title = "Select fights (Ctrl/Shift for multiple)",
                Content = panel,
                Width = 460,
                Height = 420,
                ShowInTaskbar = false,
                ShowActivated = !qa,
                WindowStartupLocation = qa ? WindowStartupLocation.Manual : WindowStartupLocation.CenterOwner,
                Left = qa ? -20000 : double.NaN,
                Top = qa ? -20000 : double.NaN,
                Opacity = qa ? 0 : 1,
                AllowsTransparency = qa,
                WindowStyle = qa ? WindowStyle.None : WindowStyle.SingleBorderWindow
            };
            apply.Click += (_, _) => { selectedFights = list.SelectedItems.Cast<EnemyOption>().Select(o => o.Id!.Value).ToArray(); window.DialogResult = true; };
            if (window.ShowDialog() == true) { fightButton.Content = $"{selectedFights!.Count} fights selected"; Apply(); }
        };
        reset.Click += (_, _) =>
        {
            _populating = true; selectedFights = null; preferences.BattleIds = null; fightButton.Content = "Select fights…";
            preferences.Player = null; preferences.Mob = null; players.SelectedItem = "(All)"; mobs.SelectedIndex = 0;
            grouped.IsChecked = true; zeroXp.IsChecked = false; detail.IsChecked = false; crystals.IsChecked = false; mode.SelectedIndex = 0; baseAttacks.SelectedItem = 1;
            _populating = false; Populate(session.GetSnapshot()); Apply();
        };
        Populate(session.GetSnapshot()); Apply();
        _subscription = session.Analytics.Subscribe(snapshot => UiThread.RunBackground(() => Populate(snapshot)));
        Unloaded += (_, _) => { _disposed = true; _subscription.Dispose(); };
        dock.Children.Add(toolbar); dock.Children.Add(queryView); Content = dock;
    }
    private sealed record EnemyOption(int? Id, string? Name, string Label) { public override string ToString() => Label; }
    private sealed record ModeOption(ReportMode Value, string Label);
    private static string ModeLabel(ReportMode mode) => mode switch
    {
        ReportMode.DamageTaken => "Damage taken",
        ReportMode.AbilityUsage => "Ability usage",
        ReportMode.AverageCuring => "Average curing",
        ReportMode.StatusCuring => "Status curing",
        ReportMode.StatusCured => "Statuses cured",
        ReportMode.DropRates => "Drop rates",
        ReportMode.CriticalRate => "Critical rate",
        _ => mode.ToString()
    };
    public static ReportMode[] Modes(string id) => id switch
    {
        "offense" => [ReportMode.All, ReportMode.Summary, ReportMode.Melee, ReportMode.Ranged, ReportMode.Other, ReportMode.Weaponskill, ReportMode.Ability, ReportMode.Spell, ReportMode.Skillchain],
        "defense" => [ReportMode.All, ReportMode.DamageTaken, ReportMode.AbilityUsage, ReportMode.Defenses, ReportMode.Utsusemi],
        "recovery" => [ReportMode.All, ReportMode.Recovery, ReportMode.Curing, ReportMode.AverageCuring, ReportMode.StatusCuring, ReportMode.StatusCured],
        "buffs" => [ReportMode.Used, ReportMode.Received],
        "debuffs" => [ReportMode.Mobs, ReportMode.Players],
        "enfeebling" => [ReportMode.All, ReportMode.Durations, ReportMode.Paralyze, ReportMode.TpMoves],
        "buffs-by-time" => [ReportMode.All, ReportMode.Accuracy, ReportMode.Attack, ReportMode.CriticalRate, ReportMode.Haste],
        "def-by-time" => [ReportMode.All, ReportMode.Accuracy, ReportMode.Attack, ReportMode.CriticalRate],
        "loot" => [ReportMode.Summary, ReportMode.DropRates, ReportMode.Stealing, ReportMode.Helm, ReportMode.Salvage],
        _ => [ReportMode.All]
    };
}
