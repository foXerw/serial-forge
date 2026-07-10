using CommunityToolkit.Mvvm.ComponentModel;

namespace SerialForge.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] private ConnectionViewModel? _connection;
    [ObservableProperty] private CommandPanelViewModel? _commandPanel;
    [ObservableProperty] private LogViewModel? _log;
}
