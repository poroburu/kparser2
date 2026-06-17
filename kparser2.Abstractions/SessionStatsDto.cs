namespace kparser2.Abstractions;

public sealed class SessionStatsDto
{
    public required long TotalPackets { get; init; }
    public required long ChatEvents { get; init; }
    public required long LootEvents { get; init; }
    public required long CombatEvents { get; init; }
    public required bool IsConnected { get; init; }
    public required string Source { get; init; }
    public int? SubscriberPackets { get; init; }
    public int? SubscriberParseErrors { get; init; }
    public int? SubscriberReconnects { get; init; }
    public string? SubscriberLastError { get; init; }
}
