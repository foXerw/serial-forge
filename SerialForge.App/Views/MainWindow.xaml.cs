using System.Windows;
using SerialForge.App.ViewModels;

namespace SerialForge.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
