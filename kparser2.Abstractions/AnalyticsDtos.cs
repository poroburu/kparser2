namespace kparser2.Abstractions;

public sealed class CombatantDto
{
    public uint Id { get; init; }
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Job { get; init; } = "";
    public string? PlayerInfo { get; init; }
}

public sealed class BattleDto
{
    public int Id { get; init; }
    public string EnemyName { get; init; } = "";
    public uint? EnemyId { get; init; }
    public long StartMs { get; init; }
    public long? EndMs { get; init; }
    public bool Killed { get; init; }
    public uint? KillerId { get; init; }
    public int ExperiencePoints { get; init; }
    public int ExperienceChain { get; init; }
}

public sealed class InteractionDto
{
    public int Id { get; init; }
    public int? BattleId { get; init; }
    public long TimestampMs { get; init; }
    public string InteractionType { get; init; } = "";
    public string? HarmType { get; init; }
    public string? AidType { get; init; }
    public string Category { get; init; } = "";
    public string DamageModifier { get; init; } = "";
    public uint ActorId { get; init; }
    public uint TargetId { get; init; }
    public string ActorName { get; init; } = "";
    public string TargetName { get; init; } = "";
    public string ActionName { get; init; } = "";
    public int Value { get; init; }
    public string Success { get; init; } = "";
    public int CommandNo { get; init; }
    public int MessageId { get; init; }
    public bool IsProc { get; init; }
    public int ProcValue { get; init; }
    public bool IsLocalPlayerActor { get; init; }
    public bool IsLocalPlayerTarget { get; init; }
}

public sealed class ChatMessageDto
{
    public long TimestampMs { get; init; }
    public string Mode { get; init; } = "";
    public int ModeId { get; init; }
    public bool IsGm { get; init; }
    public string Speaker { get; init; } = "";
    public string Message { get; init; } = "";
    public int PacketId { get; init; }
    public string Direction { get; init; } = "";
    public bool IsLocalPlayer { get; init; }
    public string? TargetName { get; init; }
}

public sealed class LootRecordDto
{
    public long TimestampMs { get; init; }
    public string EventType { get; init; } = "";
    public int ItemId { get; init; }
    public string ItemName { get; init; } = "";
    public int Quantity { get; init; }
    public int Gil { get; init; }
    public int PoolSlot { get; init; }
    public string ActorName { get; init; } = "";
    public string Detail { get; init; } = "";
}

public sealed class ItemUseDto
{
    public long TimestampMs { get; init; }
    public uint ActorId { get; init; }
    public string ActorName { get; init; } = "";
    public int ItemId { get; init; }
    public string ItemName { get; init; } = "";
    public int Quantity { get; init; }
}

public sealed class ExperienceRecordDto
{
    public long TimestampMs { get; init; }
    public uint ActorId { get; init; }
    public string ActorName { get; init; } = "";
    public int ExperiencePoints { get; init; }
    public int Chain { get; init; }
    public int? BattleId { get; init; }
}

public sealed class AnalyticsSnapshotDto
{
    public long SessionStartMs { get; init; }
    public string ZoneName { get; init; } = "";
    public IReadOnlyList<CombatantDto> Combatants { get; init; } = [];
    public IReadOnlyList<BattleDto> Battles { get; init; } = [];
    public IReadOnlyList<InteractionDto> Interactions { get; init; } = [];
    public IReadOnlyList<ChatMessageDto> ChatMessages { get; init; } = [];
    public IReadOnlyList<LootRecordDto> LootRecords { get; init; } = [];
    public IReadOnlyList<ItemUseDto> ItemUses { get; init; } = [];
    public IReadOnlyList<ExperienceRecordDto> ExperienceRecords { get; init; } = [];
}

public sealed class MobFilterDto
{
    public bool GroupMobs { get; init; } = true;
    public bool ExcludeZeroXp { get; init; }
    public string? SelectedMobName { get; init; }
    public int? SelectedBattleId { get; init; }
    public string? SelectedPlayerName { get; init; }
}

public sealed class AnalyticsRowDto
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
    public int Count { get; init; }
    public int Total { get; init; }
}

public sealed class AnalyticsReportSpanDto
{
    public string Text { get; init; } = "";
    public bool Bold { get; init; }
    public bool Underline { get; init; }
    public string Color { get; init; } = "#000000";
}

public sealed class AnalyticsReportDto
{
    public IReadOnlyList<AnalyticsReportSpanDto> Spans { get; init; } = [];
}

public sealed class ReportMetaDto
{
    public int SchemaVersion { get; init; }
    public string Title { get; init; } = "";
    public string Zone { get; init; } = "";
    public string RecordedAt { get; init; } = "";
    public string Kparser2Version { get; init; } = "";
}

public sealed class ReportBundleDto
{
    public ReportMetaDto Meta { get; init; } = new();
    public IReadOnlyList<CombatantDto> Combatants { get; init; } = [];
    public IReadOnlyList<BattleDto> Fights { get; init; } = [];
    public IReadOnlyList<InteractionDto> Events { get; init; } = [];
    public IReadOnlyList<ChatMessageDto> Chat { get; init; } = [];
    public IReadOnlyList<LootRecordDto> Loot { get; init; } = [];
    public IReadOnlyList<ItemUseDto> ItemUses { get; init; } = [];
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Summaries { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, int>>();
}
