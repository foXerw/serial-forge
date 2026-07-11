using System.Collections.Specialized;
using System.Windows.Controls;
using SerialForge.App.ViewModels;

namespace SerialForge.App.Views;

public partial class LogView : UserControl
{
    public LogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        // Re-hook the new VM's collection; unhook the old to avoid leaks/dupes.
        if (e.OldValue is LogViewModel old) old.Entries.CollectionChanged -= OnEntriesChanged;
        if (e.NewValue is LogViewModel vm) vm.Entries.CollectionChanged += OnEntriesChanged;
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is LogViewModel vm && vm.AutoScroll && LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
    }
}
