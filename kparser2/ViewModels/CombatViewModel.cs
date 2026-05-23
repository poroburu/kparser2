using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using kparser2.Abstractions;

namespace kparser2.ViewModels;

public sealed partial class CombatViewModel : ObservableObject, IDisposable
{
    private readonly IDisposable _subscription;

    public ObservableCollection<CombatEventDto> Events { get; } = [];

    public CombatViewModel(IPacketSession session)
    {
        foreach (var combat in session.GetRecentCombatEvents(500))
        {
            Events.Add(combat);
        }

        _subscription = session.CombatEvents.Subscribe(combat =>
            UiThread.Run(() =>
            {
                Events.Insert(0, combat);
                while (Events.Count > 2000)
                {
                    Events.RemoveAt(Events.Count - 1);
                }
            }));
    }

    public void Dispose() => _subscription.Dispose();
}
