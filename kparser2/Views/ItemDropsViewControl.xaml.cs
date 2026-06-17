using System.Windows.Controls;
using kparser2.Abstractions;
using kparser2.ViewModels;

namespace kparser2.Views;

public partial class ItemDropsViewControl : UserControl
{
    private readonly ItemDropsViewModel _viewModel;

    public ItemDropsViewControl(IPacketSession session)
    {
        InitializeComponent();
        _viewModel = new ItemDropsViewModel(session);
        DataContext = _viewModel;
        Unloaded += (_, _) => _viewModel.Dispose();
    }
}
