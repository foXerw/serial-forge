using System.Windows;
using SerialForge.App.ViewModels;

namespace SerialForge.App.Views;

public partial class ProtocolEditorView : Window
{
    public ProtocolEditorView() => InitializeComponent();
    public ProtocolEditorViewModel ViewModel => (ProtocolEditorViewModel)DataContext!;
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
