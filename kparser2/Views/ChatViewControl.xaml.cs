using System.Windows.Controls;
using kparser2.Abstractions;
using kparser2.ViewModels;

namespace kparser2.Views;

public partial class ChatViewControl : UserControl
{
    private readonly ChatViewModel _viewModel;

    public ChatViewControl(IPacketSession session)
    {
        InitializeComponent();
        _viewModel = new ChatViewModel(session);
        DataContext = _viewModel;
        Unloaded += (_, _) => _viewModel.Dispose();
    }
}
