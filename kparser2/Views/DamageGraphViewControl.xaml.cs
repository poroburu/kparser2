using System.Windows.Controls;
using kparser2.Abstractions;
using kparser2.ViewModels;
using LiveChartsCore.SkiaSharpView;

namespace kparser2.Views;

public partial class DamageGraphViewControl : UserControl
{
    private readonly DamageGraphViewModel _viewModel;

    public DamageGraphViewControl(IAnalyticsSession session)
    {
        InitializeComponent();
        _viewModel = new DamageGraphViewModel(session);
        DataContext = _viewModel;
        Unloaded += (_, _) => _viewModel.Dispose();
    }
}
