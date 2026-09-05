namespace kparser2.Abstractions;

public enum ReportMode
{
    All, Summary, Details, Melee, Ranged, Other, Weaponskill, Ability, Spell, Skillchain,
    DamageTaken, AbilityUsage, Defenses, Utsusemi,
    Recovery, Curing, AverageCuring, StatusCuring, StatusCured,
    Used, Received, Mobs, Players, Durations, Paralyze, TpMoves,
    Accuracy, Attack, CriticalRate, Haste, DropRates, Stealing, Helm, Salvage, Lights, Chests
}

public sealed class AnalyticsReportRequest
{
    public string QueryId { get; init; } = "offense";
    public ReportMode Mode { get; init; }
    public MobFilterDto Filter { get; init; } = new();
    // null means unrestricted; an empty selection intentionally matches no fights.
    public IReadOnlyList<int>? BattleIds { get; init; }
    public bool ShowDetails { get; init; }
    public bool ExcludeCrystals { get; init; }
    public int BaseAttacks { get; init; } = 1;
}
