using kparser2.Abstractions;

namespace kparser2.Services;

public sealed class MobFilterService
{
    public MobFilterDto Current { get; private set; } = new();

    public event Action? FilterChanged;

    public void SetGroupMobs(bool value)
    {
        Current = Clone(Current, groupMobs: value);
        FilterChanged?.Invoke();
    }

    public void SetExcludeZeroXp(bool value)
    {
        Current = Clone(Current, excludeZeroXp: value);
        FilterChanged?.Invoke();
    }

    public void SetSelectedMob(string? mobName)
    {
        Current = Clone(Current, selectedMobName: mobName);
        FilterChanged?.Invoke();
    }

    public void SetSelectedBattle(int? battleId)
    {
        Current = Clone(Current, selectedBattleId: battleId);
        FilterChanged?.Invoke();
    }

    public void SetSelectedPlayer(string? playerName)
    {
        Current = Clone(Current, selectedPlayerName: playerName);
        FilterChanged?.Invoke();
    }

    public IReadOnlyList<string> BuildMobOptions(AnalyticsSnapshotDto snapshot)
    {
        return snapshot.Battles
            .Select(b => b.EnemyName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();
    }

    public IReadOnlyList<string> BuildPlayerOptions(AnalyticsSnapshotDto snapshot)
    {
        return snapshot.Combatants
            .Where(c => c.Kind.Equals("Player", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();
    }

    private static MobFilterDto Clone(
        MobFilterDto current,
        bool? groupMobs = null,
        bool? excludeZeroXp = null,
        string? selectedMobName = null,
        int? selectedBattleId = null,
        string? selectedPlayerName = null)
    {
        return new MobFilterDto
        {
            GroupMobs = groupMobs ?? current.GroupMobs,
            ExcludeZeroXp = excludeZeroXp ?? current.ExcludeZeroXp,
            SelectedMobName = selectedMobName ?? current.SelectedMobName,
            SelectedBattleId = selectedBattleId ?? current.SelectedBattleId,
            SelectedPlayerName = selectedPlayerName ?? current.SelectedPlayerName
        };
    }
}
