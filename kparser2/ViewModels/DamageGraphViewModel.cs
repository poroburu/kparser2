using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using kparser2.Abstractions;
using kparser2.Core;
using kparser2.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace kparser2.ViewModels;

public sealed partial class DamageGraphViewModel : ObservableObject, IDisposable
{
    private readonly IAnalyticsSession _session;
    private readonly IDisposable _subscription;
    private readonly ViewSettingsService.ReportPreferences _preferences = ViewSettingsService.Shared.Report("damage-graph");
    private readonly Dictionary<string, LineSeries<double>> _seriesByPlayer = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double[]> _lastValuesByPlayer = new(StringComparer.Ordinal);
    private bool _disposed, _refreshing;
    private static readonly SKColor[] PlayerColors =
    [
        SKColors.CornflowerBlue,
        SKColors.OrangeRed,
        SKColors.SeaGreen,
        SKColors.MediumPurple,
        SKColors.Crimson,
        SKColors.DarkCyan,
        SKColors.Goldenrod,
        SKColors.SlateBlue,
        SKColors.DeepPink,
        SKColors.OliveDrab,
        SKColors.SteelBlue,
        SKColors.Chocolate
    ];
    public ObservableCollection<ISeries> Series { get; } = [];
    public ObservableCollection<string> Players { get; } = ["All"];
    public ObservableCollection<string> Enemies { get; } = ["All"];
    public string[] Categories { get; } = ["All", "Melee", "Ranged", "Weaponskill", "Spell", "Ability", "Skillchain"];
    public int[] Intervals { get; } = [1, 5, 10, 30, 60];
    public Axis[] XAxes { get; } = [new() { Name = "Elapsed seconds", MinStep = 1 }];
    public Axis[] YAxes { get; } = [new() { Name = "Damage", MinLimit = 0 }];
    [ObservableProperty] private string _selectedPlayer = "All";
    [ObservableProperty] private string _selectedEnemy = "All";
    [ObservableProperty] private string _selectedCategory = "All";
    [ObservableProperty] private bool _cumulative;
    [ObservableProperty] private bool _excludeZeroXp;
    [ObservableProperty] private int _bucketSeconds = 10;
    [ObservableProperty] private string _status = "";
    public DamageGraphViewModel(IAnalyticsSession session)
    {
        _session = session;
        _selectedPlayer = _preferences.Player ?? "All"; _selectedEnemy = _preferences.Mob ?? "All";
        _selectedCategory = Categories.Contains(_preferences.Category) ? _preferences.Category : "All";
        _cumulative = _preferences.Cumulative; _bucketSeconds = Intervals.Contains(_preferences.BucketSeconds) ? _preferences.BucketSeconds : 10;
        _excludeZeroXp = _preferences.ExcludeZeroXp;
        Refresh(session.GetSnapshot());
        _subscription = session.Analytics.Subscribe(snapshot => UiThread.RunBackground(() => Refresh(snapshot)));
    }
    partial void OnSelectedPlayerChanged(string value) => Changed();
    partial void OnSelectedEnemyChanged(string value) => Changed();
    partial void OnSelectedCategoryChanged(string value) => Changed();
    partial void OnBucketSecondsChanged(int value) => Changed();
    partial void OnCumulativeChanged(bool value) => Changed();
    partial void OnExcludeZeroXpChanged(bool value) => Changed();
    private void Changed()
    {
        if (_refreshing || _disposed) return;
        _preferences.Player = SelectedPlayer; _preferences.Mob = SelectedEnemy; _preferences.Category = SelectedCategory;
        _preferences.Cumulative = Cumulative; _preferences.BucketSeconds = BucketSeconds; _preferences.ExcludeZeroXp = ExcludeZeroXp;
        ViewSettingsService.Shared.Save(); Refresh(_session.GetSnapshot());
    }
    private void Refresh(AnalyticsSnapshotDto snapshot)
    {
        if (_disposed) return;
        _refreshing = true;
        try
        {
            var participants = snapshot.Combatants.Where(c => c.Kind is "Player" or "Pet" or "Fellow").Select(c => c.Id).ToHashSet();
            foreach (var name in snapshot.Combatants.Where(c => participants.Contains(c.Id)).Select(c => c.Name).Append(SelectedPlayer))
                if (!Players.Contains(name)) Players.Add(name);
            foreach (var name in snapshot.Battles.Select(b => b.EnemyName).Append(SelectedEnemy))
                if (!Enemies.Contains(name)) Enemies.Add(name);
            var battles = snapshot.Battles.Where(b => (SelectedEnemy == "All" || b.EnemyName == SelectedEnemy) && (!ExcludeZeroXp || b.ExperiencePoints > 0)).Select(b => b.Id).ToHashSet();
            var restricted = SelectedEnemy != "All" || ExcludeZeroXp;
            var rows = snapshot.Interactions.Where(i => i.InteractionType == "Harm" && i.HarmType != "Enfeeble" && (participants.Contains(i.ActorId) || i.IsLocalPlayerActor)
                && (SelectedPlayer == "All" || i.ActorName == SelectedPlayer)
                && (!restricted || i.BattleId is int id && battles.Contains(id))
                && (SelectedCategory == "All" || i.Category.Replace(" Crit", "") == SelectedCategory)).ToArray();
            if (rows.Length == 0)
            {
                ReconcileSeries([]);
                Status = "No matching damage in this capture.";
                return;
            }
            var start = rows.Min(i => i.TimestampMs);
            var duration = rows.Max(i => i.TimestampMs) - start;
            // Bound chart work for multi-day captures, making the effective interval explicit.
            var step = Math.Max(BucketSeconds * 1000L, (duration + 1999) / 2000);
            var count = checked((int)(duration / step)) + 1;
            var labels = Enumerable.Range(0, count).Select(i => (i * step / 1000.0).ToString("0.#")).ToArray();
            var currentLabels = XAxes[0].Labels;
            if (currentLabels is null || !currentLabels.SequenceEqual(labels)) XAxes[0].Labels = labels;
            var desiredSeries = new List<ISeries>();
            foreach (var player in rows.GroupBy(i => i.ActorName).OrderBy(g => g.Key))
            {
                var values = new double[count];
                foreach (var row in player) values[(int)((row.TimestampMs - start) / step)] += Math.Max(0, row.Value);
                if (Cumulative) for (var i = 1; i < values.Length; i++) values[i] += values[i - 1];
                if (!_seriesByPlayer.TryGetValue(player.Key, out var series))
                {
                    series = new LineSeries<double>
                    {
                        Name = player.Key,
                        GeometrySize = 0,
                        LineSmoothness = 0,
                        Stroke = new SolidColorPaint(ColorFor(player.Key), 2),
                        Fill = null,
                        AnimationsSpeed = TimeSpan.Zero
                    };
                    _seriesByPlayer[player.Key] = series;
                }

                if (!_lastValuesByPlayer.TryGetValue(player.Key, out var previous) || !previous.SequenceEqual(values))
                {
                    series.Values = values;
                    _lastValuesByPlayer[player.Key] = values;
                }

                desiredSeries.Add(series);
            }
            ReconcileSeries(desiredSeries);
            Status = $"{(Cumulative ? "Cumulative" : "Individual interval")} damage · {step / 1000.0:0.##} second intervals";
        }
        finally { _refreshing = false; }
    }

    private static SKColor ColorFor(string player)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in player)
            {
                hash ^= char.ToUpperInvariant(character);
                hash *= 16777619;
            }

            return PlayerColors[hash % (uint)PlayerColors.Length];
        }
    }

    private void ReconcileSeries(IReadOnlyList<ISeries> desired)
    {
        for (var index = 0; index < desired.Count; index++)
        {
            if (index < Series.Count)
            {
                if (!ReferenceEquals(Series[index], desired[index])) Series[index] = desired[index];
            }
            else
            {
                Series.Add(desired[index]);
            }
        }

        while (Series.Count > desired.Count) Series.RemoveAt(Series.Count - 1);
    }

    public void Dispose() { _disposed = true; _subscription.Dispose(); }
}
