using System.Diagnostics;
using System.Text.Json;
using System.Windows.Controls;
using kparser2.Abstractions;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WPF;
using LiveChartsCore.SkiaSharpView.Painting;

namespace kparser2.Ui.Qa;

internal sealed partial class ReplayQa
{
    private async Task CheckGraphAsync(UserControl control, IAnalyticsSession session, AnalyticsSnapshotDto snapshot)
    {
        var player = Find<ComboBox>(control, "graph-player")!;
        var enemy = Find<ComboBox>(control, "graph-enemy")!;
        var category = Find<ComboBox>(control, "graph-category")!;
        var interval = Find<ComboBox>(control, "graph-interval")!;
        var cumulative = Find<CheckBox>(control, "graph-cumulative")!;
        var zeroXp = Find<CheckBox>(control, "graph-exclude-zero-xp")!;
        async Task Check(string scenario, AnalyticsSnapshotDto? state = null)
        {
            var timer = Stopwatch.StartNew();
            await PumpAsync();
            var chart = Descendants(control).OfType<CartesianChart>().Single();
            var input = state ?? snapshot;
            var actors = input.Combatants.Where(c => c.Kind is "Player" or "Pet" or "Fellow").Select(c => c.Id).ToHashSet();
            var selectedPlayer = (string)player.SelectedItem;
            var selectedEnemy = (string)enemy.SelectedItem;
            var selectedCategory = (string)category.SelectedItem;
            var qualifyingBattles = input.Battles.Where(b =>
                (selectedEnemy == "All" || b.EnemyName == selectedEnemy) && (zeroXp.IsChecked != true || b.ExperiencePoints > 0))
                .Select(b => b.Id).ToHashSet();
            var rows = input.Interactions.Where(i =>
                i.InteractionType == "Harm" && i.HarmType != "Enfeeble" && (actors.Contains(i.ActorId) || i.IsLocalPlayerActor) &&
                (selectedPlayer == "All" || i.ActorName == selectedPlayer) &&
                (selectedCategory == "All" || i.Category.Replace(" Crit", "") == selectedCategory) &&
                ((selectedEnemy == "All" && zeroXp.IsChecked != true) || (i.BattleId is int id && qualifyingBattles.Contains(id))))
                .ToArray();
            var expectedTotals = rows.GroupBy(r => r.ActorName).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new Total(g.Key, g.Sum(r => (double)Math.Max(0, r.Value)))).ToArray();
            // Independent conservation checks: interval totals (or the final cumulative
            // value) must equal input damage. Empty refresh must clear both data and axes.
            var series = chart.Series.OfType<LineSeries<double>>().OrderBy(s => s.Name, StringComparer.Ordinal).ToArray();
            var values = series.Select(s => s.Values?.ToArray() ?? []).ToArray();
            var actualTotals = series.Select((s, index) => new Total(s.Name ?? "", cumulative.IsChecked == true
                ? values[index].LastOrDefault() : values[index].Sum())).ToArray();
            var labels = chart.XAxes.OfType<Axis>().Single().Labels?.ToArray() ?? [];
            var legendMatches = series.All(s => s.Stroke is SolidColorPaint stroke &&
                s.GeometryStroke is SolidColorPaint marker && s.GeometryFill is SolidColorPaint fill &&
                stroke.Color == marker.Color && stroke.Color == fill.Color);
            var expected = JsonSerializer.Serialize(new GraphResult(expectedTotals, true, true, rows.Length == 0, true), Json);
            var actual = JsonSerializer.Serialize(new GraphResult(actualTotals,
                values.All(v => v.All(n => n >= 0) && (cumulative.IsChecked != true || v.Zip(v.Skip(1), (a,b) => b >= a).All(x => x))),
                values.All(v => v.Length == labels.Length), labels.Length == 0, legendMatches), Json);
            // Let the chart's drawing scheduler consume the bound series before saving
            // its viewport. Numerical success never substitutes for screenshot review.
            control.UpdateLayout();
            await Task.Delay(150);
            await PumpAsync();
            SaveCase("damage-graph", scenario, control, expected, actual,
                new { player = selectedPlayer, enemy = selectedEnemy, category = selectedCategory, interval = interval.SelectedItem, cumulative = cumulative.IsChecked, exclude_zero_xp = zeroXp.IsChecked },
                rows.Length == 0 ? "empty-or-partial" : "populated", timer.ElapsedMilliseconds, actual == expected && chart.ActualWidth > 0 && chart.ActualHeight > 0);
        }
        player.SelectedItem = "All"; enemy.SelectedItem = "All"; category.SelectedItem = "All";
        interval.SelectedItem = 10; cumulative.IsChecked = false; zeroXp.IsChecked = false;
        await Check("default");
        _window.Width = 760; _window.Height = 480;
        await Check("compact");
        _window.Width = 1200; _window.Height = 800;
        if (player.Items.Count > 1) { player.SelectedIndex = 1; await Check("player-filter"); player.SelectedItem = "All"; }
        if (enemy.Items.Count > 1) { enemy.SelectedIndex = 1; await Check("enemy-filter"); enemy.SelectedItem = "All"; }
        foreach (var value in new[] { "Melee", "Spell", "Weaponskill" })
        { category.SelectedItem = value; await Check("category-" + value); }
        category.SelectedItem = "All";
        foreach (var value in new[] { 1, 30, 60 }) { interval.SelectedItem = value; await Check("interval-" + value); }
        cumulative.IsChecked = true;
        await Check("cumulative");
        zeroXp.IsChecked = true;
        await Check("exclude-zero-xp");
        zeroXp.IsChecked = false;
        session.LoadSnapshot(new AnalyticsSnapshotDto());
        await Check("empty-refresh", new AnalyticsSnapshotDto());
        session.LoadSnapshot(snapshot); session.LoadSnapshot(snapshot);
        await Check("repeat-refresh");
    }

    private sealed record Total(string Player, double Damage);
    private sealed record GraphResult(Total[] Totals, bool NonnegativeAndMonotonic, bool AxisMatchesSeries, bool AxesEmpty, bool LegendMatchesLines);
}
