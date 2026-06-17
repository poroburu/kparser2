namespace kparser2.Services;

public sealed class ViewRegistry
{
    private readonly List<IPacketView> _packetViews = [];
    private readonly List<IAnalyticsView> _analyticsViews = [];

    public ViewRegistry Register(IPacketView view)
    {
        _packetViews.Add(view);
        return this;
    }

    public ViewRegistry Register(IAnalyticsView view)
    {
        _analyticsViews.Add(view);
        return this;
    }

    public IReadOnlyList<IPacketView> PacketViews => _packetViews;
    public IReadOnlyList<IAnalyticsView> AnalyticsViews => _analyticsViews;

    public IReadOnlyList<IAnalyticsView> AllAnalyticsViews => _analyticsViews;
}

public static class ViewRegistryFactory
{
    public static ViewRegistry CreateDefault()
    {
        var registry = new ViewRegistry()
            .Register(new Views.DebugView())
            .Register(new Views.PacketMonitorView());

        foreach (var view in Views.AnalyticsViewCatalog.All)
        {
            registry.Register(view);
        }

        return registry;
    }
}
