using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using kparser2.Abstractions;

namespace kparser2.ViewModels;

public sealed partial class ChatViewModel : ObservableObject, IDisposable
{
    private readonly IDisposable _subscription;

    public ObservableCollection<ChatEventDto> Events { get; } = [];

    public ChatViewModel(IPacketSession session)
    {
        foreach (var chat in session.GetRecentChatEvents(500))
        {
            Events.Add(chat);
        }

        _subscription = session.ChatEvents.Subscribe(chat =>
            UiThread.Run(() =>
            {
                Events.Insert(0, chat);
                while (Events.Count > 2000)
                {
                    Events.RemoveAt(Events.Count - 1);
                }
            }));
    }

    public void Dispose() => _subscription.Dispose();
}
