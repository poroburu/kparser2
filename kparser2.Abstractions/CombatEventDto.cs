namespace kparser2.Abstractions;

public sealed class CombatEventDto
{
    public required ulong Timestamp { get; init; }
    public required int PacketId { get; init; }
    public required string EventKind { get; init; }
    public required uint ActorId { get; init; }
    public required uint TargetId { get; init; }
    public required int CommandNo { get; init; }
    public required uint CommandArg { get; init; }
    public required int MessageNum { get; init; }
    public required int MessageType { get; init; }
    public required int Param1 { get; init; }
    public required int Param2 { get; init; }
    public required int Miss { get; init; }
    public required int Damage { get; init; }
    public required int MessageId { get; init; }
    public required string Summary { get; init; }
}
