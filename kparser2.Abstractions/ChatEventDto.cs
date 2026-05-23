namespace kparser2.Abstractions;

public sealed class ChatEventDto
{
    public required ulong Timestamp { get; init; }
    public required string Direction { get; init; }
    public required int PacketId { get; init; }
    public required string Speaker { get; init; }
    public required string Message { get; init; }
}
