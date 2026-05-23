using kparser2.Abstractions;
using kparser2.ViewModels;

namespace kparser2.Views;

public partial class CombatViewControl : System.Windows.Controls.UserControl
{
    public CombatViewControl(IPacketSession session)
    {
        InitializeComponent();
        DataContext = new CombatViewModel(session);
    }
}
