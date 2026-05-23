namespace kparser2.Abstractions;

public sealed class LootEventDto
{
    public required ulong Timestamp { get; init; }
    public required int PacketId { get; init; }
    public required string EventType { get; init; }
    public required int ItemId { get; init; }
    public required string ItemName { get; init; }
    public required int Quantity { get; init; }
    public required int Gil { get; init; }
    public required int PoolSlot { get; init; }
    public required string ActorName { get; init; }
    public required string Source { get; init; }
    public required string Detail { get; init; }
}
