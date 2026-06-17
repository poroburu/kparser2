using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using kparser2.Abstractions;

namespace kparser2.ViewModels;

public sealed partial class ItemDropsViewModel : ObservableObject, IDisposable
{
    private readonly IDisposable _subscription;

    public ObservableCollection<LootEventDto> Events { get; } = [];

    public ItemDropsViewModel(IPacketSession session)
    {
        foreach (var loot in session.GetRecentLootEvents(500))
        {
            Events.Add(loot);
        }

        _subscription = session.LootEvents.Subscribe(loot =>
            UiThread.Run(() =>
            {
                Events.Insert(0, loot);
                while (Events.Count > 2000)
                {
                    Events.RemoveAt(Events.Count - 1);
                }
            }));
    }

    public void Dispose() => _subscription.Dispose();
}
