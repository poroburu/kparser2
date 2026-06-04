using System.Windows.Controls;

namespace kparser2;

public interface IAnalyticsView
{
    string Id { get; }
    string Title { get; }
    bool IsDebug { get; }
    UserControl CreateView(Abstractions.IAnalyticsSession session);
}
