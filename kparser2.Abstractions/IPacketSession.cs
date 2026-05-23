namespace kparser2.Abstractions;

public interface IPacketSession : IDisposable
{
    IObservable<PacketRowDto> Packets { get; }
    IObservable<ChatEventDto> ChatEvents { get; }
    IObservable<LootEventDto> LootEvents { get; }
    Task<SessionStatsDto> GetStatsAsync();
    IReadOnlyList<PacketRowDto> GetRecentPackets(int count = 100);
    IReadOnlyList<ChatEventDto> GetRecentChatEvents(int count = 100);
    IReadOnlyList<LootEventDto> GetRecentLootEvents(int count = 100);
    PacketRowDto? GetSelectedPacket();
    void SelectPacket(PacketRowDto? packet);
}
