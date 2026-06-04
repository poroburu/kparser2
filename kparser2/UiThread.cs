using System.Windows;
using System.Windows.Threading;

namespace kparser2;

internal static class UiThread
{
    public static void Run(Action action) => Run(DispatcherPriority.Normal, action);

    public static void RunBackground(Action action) => Run(DispatcherPriority.Background, action);

    private static void Run(DispatcherPriority priority, Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(priority, action);
    }
}
