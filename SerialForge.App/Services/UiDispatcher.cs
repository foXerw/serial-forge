using System.Windows.Threading;

namespace SerialForge.App.Services;

public static class UiDispatcher
{
    public static Dispatcher Current { get; } = Dispatcher.CurrentDispatcher;
    public static void Marshal(Action a) => Current.BeginInvoke(a);
}
