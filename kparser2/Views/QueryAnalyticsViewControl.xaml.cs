using System.Windows.Controls;
using kparser2.Abstractions;
using kparser2.Services;
using kparser2.ViewModels;

namespace kparser2.Views;

public partial class QueryAnalyticsViewControl : UserControl
{
    private readonly QueryAnalyticsViewModel _viewModel;

    public QueryAnalyticsViewControl(IAnalyticsSession session, string queryId, MobFilterService? mobFilter = null)
    {
        InitializeComponent();
        _viewModel = new QueryAnalyticsViewModel(session, queryId, mobFilter);
        DataContext = _viewModel;
        Unloaded += (_, _) => _viewModel.Dispose();
    }
}
