using System.Windows.Controls;
using kparser2.Abstractions;

namespace kparser2.Views;

public sealed class QueryAnalyticsView(string id, string title, string queryId, bool isDebug = false, bool combat = false)
    : IAnalyticsView
{
    public string Id => id;
    public string Title => title;
    public bool IsDebug => isDebug;

    public UserControl CreateView(IAnalyticsSession session) =>
        combat
            ? new CombatAnalyticsViewControl(session, queryId)
            : new QueryAnalyticsViewControl(session, queryId);
}

public sealed class ChatAnalyticsView : IAnalyticsView
{
    public string Id => "chat";
    public string Title => "Chat";
    public bool IsDebug => false;

    public UserControl CreateView(IAnalyticsSession session) => new ChatAnalyticsViewControl(session);
}

public sealed class DamageGraphAnalyticsView : IAnalyticsView
{
    public string Id => "damage-graph";
    public string Title => "Damage Graph";
    public bool IsDebug => false;

    public UserControl CreateView(IAnalyticsSession session) => new DamageGraphViewControl(session);
}

public sealed class RawDataAnalyticsView : IAnalyticsView
{
    public string Id => "raw-data";
    public string Title => "Raw Data";
    public bool IsDebug => true;

    public UserControl CreateView(IAnalyticsSession session) => new DebugViewControl(session);
}

public static class AnalyticsViewCatalog
{
    public static IReadOnlyList<IAnalyticsView> All { get; } =
    [
        new ChatAnalyticsView(),
        new QueryAnalyticsView("loot", "Loot", "loot"),
        new QueryAnalyticsView("items", "Items", "items"),
        new QueryAnalyticsView("player-info", "Player Info", "players"),
        new QueryAnalyticsView("experience", "Experience", "experience"),
        new QueryAnalyticsView("fights", "Fights", "fights", combat: true),
        new QueryAnalyticsView("offense", "Offense", "offense", combat: true),
        new QueryAnalyticsView("offense-detail", "Offense Detail", "offense-detail", combat: true),
        new QueryAnalyticsView("defense", "Defense", "defense", combat: true),
        new QueryAnalyticsView("defense-detail", "Defense Details", "defense-detail", combat: true),
        new QueryAnalyticsView("deaths", "Deaths", "deaths", combat: true),
        new QueryAnalyticsView("recovery", "Recovery", "recovery", combat: true),
        new QueryAnalyticsView("buffs", "Buffs", "buffs", combat: true),
        new QueryAnalyticsView("debuffs", "Debuffs", "debuffs", combat: true),
        new QueryAnalyticsView("enfeebling", "Enfeebling", "enfeebling", combat: true),
        new QueryAnalyticsView("buffs-by-time", "Buffs by Time", "buffs-by-time", combat: true),
        new QueryAnalyticsView("def-by-time", "Def. by Time", "def-by-time", combat: true),
        new QueryAnalyticsView("extra-attacks", "Extra Attacks", "extra-attacks", combat: true),
        new QueryAnalyticsView("add-effect", "Add'l Effect", "add-effect", combat: true),
        new QueryAnalyticsView("ws-rates", "WS Rates", "ws-rates", combat: true),
        new QueryAnalyticsView("thief", "Thief", "thief", combat: true),
        new QueryAnalyticsView("corsair", "Corsair", "corsair", combat: true),
        new QueryAnalyticsView("performance", "Performance", "performance", combat: true),
        new QueryAnalyticsView("abyssea", "Abyssea", "abyssea"),
        new QueryAnalyticsView("skillchain", "Skillchain", "skillchain", combat: true),
        new QueryAnalyticsView("chat-summary", "Chat Summary", "chat-summary"),
        new DamageGraphAnalyticsView(),
        new RawDataAnalyticsView()
    ];
}
