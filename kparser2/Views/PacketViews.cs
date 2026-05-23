using System.Windows.Controls;
using kparser2.Abstractions;

namespace kparser2.Views;

public sealed class DebugView : IPacketView
{
    public string Id => "debug";
    public string Title => "Debug";

    public UserControl CreateView(IPacketSession session) => new DebugViewControl(session);
}

public sealed class PacketMonitorView : IPacketView
{
    public string Id => "monitor";
    public string Title => "Packet Monitor";

    public UserControl CreateView(IPacketSession session) => new PacketMonitorViewControl(session);
}

public sealed class ChatView : IPacketView
{
    public string Id => "chat";
    public string Title => "Chat";

    public UserControl CreateView(IPacketSession session) => new ChatViewControl(session);
}

public sealed class ItemDropsView : IPacketView
{
    public string Id => "drops";
    public string Title => "Item Drops";

    public UserControl CreateView(IPacketSession session) => new ItemDropsViewControl(session);
}
