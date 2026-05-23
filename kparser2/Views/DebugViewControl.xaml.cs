using System.Windows.Controls;
using kparser2.Abstractions;
using kparser2.ViewModels;

namespace kparser2.Views;

public partial class DebugViewControl : UserControl
{
    private readonly DebugViewModel _viewModel;

    public DebugViewControl(IPacketSession session)
    {
        InitializeComponent();
        _viewModel = new DebugViewModel(session);
        DataContext = _viewModel;
        Unloaded += (_, _) => _viewModel.Dispose();
    }
}
