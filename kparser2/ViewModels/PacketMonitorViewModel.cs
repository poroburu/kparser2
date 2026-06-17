using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using kparser2.Abstractions;

namespace kparser2.ViewModels;

public sealed partial class PacketMonitorViewModel : ObservableObject, IDisposable
{
    private readonly IPacketSession _session;
    private readonly IDisposable _subscription;

    public ObservableCollection<PacketRowDto> Rows { get; } = [];

    [ObservableProperty]
    private string _statusText = "Starting…";

    public PacketMonitorViewModel(IPacketSession session)
    {
        _session = session;
        foreach (var packet in session.GetRecentPackets(200))
        {
            Rows.Add(packet);
        }

        _subscription = session.Packets.Subscribe(packet =>
            UiThread.Run(() =>
            {
                Rows.Insert(0, packet);
                while (Rows.Count > 2000)
                {
                    Rows.RemoveAt(Rows.Count - 1);
                }
            }));

        _ = RefreshStatsAsync();
    }

    private async Task RefreshStatsAsync()
    {
        var stats = await _session.GetStatsAsync().ConfigureAwait(false);
        UiThread.Run(() =>
            StatusText = $"{stats.Source} | packets: {stats.TotalPackets} | chat: {stats.ChatEvents} | loot: {stats.LootEvents}");
    }

    public void Dispose() => _subscription.Dispose();
}
