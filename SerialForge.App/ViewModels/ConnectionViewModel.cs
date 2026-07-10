using System.IO.Ports;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialForge.Transport;

namespace SerialForge.App.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    private readonly Func<SerialTransportOptions, ITransport> _factory;
    private ITransport? _transport;
    private RelayCommand? _disconnectCommand;

    public string[] PortNames { get; } = SerialPort.GetPortNames().DefaultIfEmpty("COM1").ToArray();
    public int[] BaudRates { get; } = new[] { 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600 };

    [ObservableProperty] private string _portName = "COM1";
    [ObservableProperty] private int _baudRate = 115200;
    [ObservableProperty] private string _status = "Disconnected";
    [ObservableProperty] private bool _isConnected;

    public ITransport? Transport => _transport;

    public ICommand Connect { get; }
    public ICommand Disconnect { get; }

    public ConnectionViewModel() : this(_ => new SerialTransport(_)) { } // design-time / production
    public ConnectionViewModel(Func<SerialTransportOptions, ITransport> factory)
    {
        _factory = factory;
        Connect = new RelayCommand(() => DoConnect());
        _disconnectCommand = new RelayCommand(() => DoDisconnect(), () => IsConnected);
        Disconnect = _disconnectCommand;
    }

    partial void OnIsConnectedChanged(bool value) => _disconnectCommand?.NotifyCanExecuteChanged();

    private void DoConnect()
    {
        _transport?.Dispose();
        _transport = _factory(new SerialTransportOptions(PortName, BaudRate));
        try
        {
            _transport.Open();
            Status = "Connected";
            IsConnected = true;
            // The live transport changed: notify so MainViewModel re-subscribes
            // BytesReceived on the new instance. IsConnected alone is not enough
            // (reconnecting while already connected never toggles IsConnected).
            OnPropertyChanged(nameof(Transport));
        }
        catch (Exception ex) { Status = "Error: " + ex.Message; }
    }

    private void DoDisconnect()
    {
        _transport?.Dispose();   // release the COM port
        _transport = null;
        Status = "Disconnected";
        IsConnected = false;
        OnPropertyChanged(nameof(Transport));
    }
}
