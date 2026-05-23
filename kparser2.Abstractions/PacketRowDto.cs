namespace kparser2.Abstractions;

public sealed class PacketRowDto
{
    public required string Topic { get; init; }
    public required ulong Timestamp { get; init; }
    public required string Direction { get; init; }
    public required string PacketType { get; init; }
    public required int PacketId { get; init; }
    public required string PacketName { get; init; }
    public required int Size { get; init; }
    public required bool Injected { get; init; }
    public required bool Blocked { get; init; }
    public required ulong MessageId { get; init; }
    public byte[]? Data { get; init; }
}
