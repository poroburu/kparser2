using System.Windows.Controls;
using kparser2.Abstractions;
using kparser2.ViewModels;

namespace kparser2.Views;

public partial class PacketMonitorViewControl : UserControl
{
    private readonly PacketMonitorViewModel _viewModel;

    public PacketMonitorViewControl(IPacketSession session)
    {
        InitializeComponent();
        _viewModel = new PacketMonitorViewModel(session);
        DataContext = _viewModel;
        Unloaded += (_, _) => _viewModel.Dispose();
    }
}
