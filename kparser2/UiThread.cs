using System.Windows;
using System.Windows.Threading;

namespace kparser2;

internal static class UiThread
{
    public static void Run(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
    }
}
