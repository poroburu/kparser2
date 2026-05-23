namespace kparser2.Services;

public sealed class ViewRegistry
{
    private readonly List<IPacketView> _views = [];

    public ViewRegistry Register(IPacketView view)
    {
        _views.Add(view);
        return this;
    }

    public IReadOnlyList<IPacketView> Views => _views;
}

public static class ViewRegistryFactory
{
    public static ViewRegistry CreateDefault()
    {
        return new ViewRegistry()
            .Register(new Views.DebugView())
            .Register(new Views.PacketMonitorView())
            .Register(new Views.ChatView())
            .Register(new Views.ItemDropsView());
    }
}
