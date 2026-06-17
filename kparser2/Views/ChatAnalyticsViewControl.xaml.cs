using System.Windows.Controls;
using kparser2.Abstractions;
using kparser2.ViewModels;

namespace kparser2.Views;

public partial class ChatAnalyticsViewControl : UserControl
{
    private readonly ChatAnalyticsViewModel _viewModel;

    public ChatAnalyticsViewControl(IAnalyticsSession session)
    {
        InitializeComponent();
        _viewModel = new ChatAnalyticsViewModel(session);
        DataContext = _viewModel;
        Unloaded += (_, _) => _viewModel.Dispose();
    }
}
