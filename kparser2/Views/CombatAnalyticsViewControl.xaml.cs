using System.Windows;
using System.Windows.Controls;
using kparser2.Abstractions;
using kparser2.Services;

namespace kparser2.Views;

public sealed class CombatAnalyticsViewControl : UserControl
{
    private readonly MobFilterService _mobFilter = new();
    private bool _isPopulatingFilters;

    public CombatAnalyticsViewControl(IAnalyticsSession session, string queryId)
    {
        var dock = new DockPanel { Margin = new Thickness(8) };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(toolbar, Dock.Top);

        var groupMobs = new CheckBox { Content = "Group mobs", IsChecked = true, Margin = new Thickness(0, 0, 12, 0) };
        var excludeZeroXp = new CheckBox { Content = "Exclude 0 XP", Margin = new Thickness(0, 0, 12, 0) };
        var mobCombo = new ComboBox { Width = 180 };
        var playerCombo = new ComboBox { Width = 140 };

        toolbar.Children.Add(groupMobs);
        toolbar.Children.Add(excludeZeroXp);
        toolbar.Children.Add(new TextBlock { Text = "Mob:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        toolbar.Children.Add(mobCombo);
        toolbar.Children.Add(new TextBlock { Text = "Player:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 4, 0) });
        toolbar.Children.Add(playerCombo);

        var queryView = new QueryAnalyticsViewControl(session, queryId, _mobFilter);

        groupMobs.Checked += (_, _) => _mobFilter.SetGroupMobs(true);
        groupMobs.Unchecked += (_, _) => _mobFilter.SetGroupMobs(false);
        excludeZeroXp.Checked += (_, _) => _mobFilter.SetExcludeZeroXp(true);
        excludeZeroXp.Unchecked += (_, _) => _mobFilter.SetExcludeZeroXp(false);

        mobCombo.SelectionChanged += (_, _) =>
        {
            if (_isPopulatingFilters)
            {
                return;
            }

            var value = mobCombo.SelectedItem as string;
            _mobFilter.SetSelectedMob(value is null or "(All)" ? null : value);
        };

        playerCombo.SelectionChanged += (_, _) =>
        {
            if (_isPopulatingFilters)
            {
                return;
            }

            var value = playerCombo.SelectedItem as string;
            _mobFilter.SetSelectedPlayer(value is null or "(All)" ? null : value);
        };

        void SelectComboDefault(ComboBox combo, IReadOnlyList<string> options, string? previous, Action<string?> applyFilter)
        {
            if (!string.IsNullOrWhiteSpace(previous)
                && previous != "(All)"
                && options.Contains(previous, StringComparer.OrdinalIgnoreCase))
            {
                combo.SelectedItem = options.First(o => o.Equals(previous, StringComparison.OrdinalIgnoreCase));
                applyFilter(previous);
                return;
            }

            combo.SelectedIndex = 0;
            applyFilter(null);
        }

        void PopulateFilters(AnalyticsSnapshotDto snapshot)
        {
            UiThread.RunBackground(() =>
            {
                _isPopulatingFilters = true;
                try
                {
                    var previousMob = mobCombo.SelectedItem as string;
                    var previousPlayer = playerCombo.SelectedItem as string;

                    var mobOptions = new[] { "(All)" }.Concat(_mobFilter.BuildMobOptions(snapshot)).ToList();
                    var playerOptions = new[] { "(All)" }.Concat(_mobFilter.BuildPlayerOptions(snapshot)).ToList();

                    mobCombo.ItemsSource = mobOptions;
                    playerCombo.ItemsSource = playerOptions;

                    SelectComboDefault(mobCombo, mobOptions, previousMob, mob => _mobFilter.SetSelectedMob(mob));
                    SelectComboDefault(playerCombo, playerOptions, previousPlayer, player => _mobFilter.SetSelectedPlayer(player));
                }
                finally
                {
                    _isPopulatingFilters = false;
                }
            });
        }

        PopulateFilters(session.GetSnapshot());
        session.Analytics.Subscribe(PopulateFilters);

        dock.Children.Add(toolbar);
        dock.Children.Add(queryView);
        Content = dock;
    }
}
