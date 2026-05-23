using System.Windows.Controls;

namespace kparser2;

public interface IPacketView
{
    string Id { get; }
    string Title { get; }
    UserControl CreateView(kparser2.Abstractions.IPacketSession session);
}
