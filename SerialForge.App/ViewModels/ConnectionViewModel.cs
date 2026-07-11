using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialForge.Transport;

namespace SerialForge.App.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    private const string DefaultSettingsFile = "serialforge.settings.json";
    private readonly Func<SerialTransportOptions, ITransport> _factory;
    private readonly string? _settingsPath;
    private ITransport? _transport;
    private RelayCommand? _disconnectCommand;

    public string[] PortNames { get; } = SerialPort.GetPortNames().DefaultIfEmpty("COM1").ToArray();
    public int[] BaudRates { get; } = new[] { 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600 };

    [ObservableProperty] private string _portName = "COM1";
    [ObservableProperty] private int _baudRate = 115200;
    [ObservableProperty] private string _status = "未连接";
    [ObservableProperty] private bool _isConnected;

    public ITransport? Transport => _transport;

    public ICommand Connect { get; }
    public ICommand Disconnect { get; }

    // Production: persist last port/baud next to the exe.
    public ConnectionViewModel() : this(_ => new SerialTransport(_), Path.Combine(AppContext.BaseDirectory, DefaultSettingsFile)) { }
    // Tests: no persistence (null path) — no stray settings file in the bin dir.
    public ConnectionViewModel(Func<SerialTransportOptions, ITransport> factory) : this(factory, null) { }
    public ConnectionViewModel(Func<SerialTransportOptions, ITransport> factory, string? settingsPath)
    {
        _factory = factory;
        _settingsPath = settingsPath;
        Connect = new RelayCommand(() => DoConnect());
        _disconnectCommand = new RelayCommand(() => DoDisconnect(), () => IsConnected);
        Disconnect = _disconnectCommand;
        LoadSettings();
    }

    partial void OnIsConnectedChanged(bool value) => _disconnectCommand?.NotifyCanExecuteChanged();

    private void DoConnect()
    {
        _transport?.Dispose();
        _transport = _factory(new SerialTransportOptions(PortName, BaudRate));
        try
        {
            _transport.Open();
            Status = "已连接";
            IsConnected = true;
            SaveSettings();
            // The live transport changed: notify so MainViewModel re-subscribes
            // BytesReceived on the new instance. IsConnected alone is not enough
            // (reconnecting while already connected never toggles IsConnected).
            OnPropertyChanged(nameof(Transport));
        }
        catch (Exception ex) { Status = "错误：" + ex.Message; }
    }

    private void DoDisconnect()
    {
        _transport?.Dispose();   // release the COM port
        _transport = null;
        Status = "未连接";
        IsConnected = false;
        OnPropertyChanged(nameof(Transport));
    }

    private void LoadSettings()
    {
        if (_settingsPath is null || !File.Exists(_settingsPath)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_settingsPath));
            if (doc.RootElement.TryGetProperty("portName", out var p) && p.ValueKind == JsonValueKind.String)
                PortName = p.GetString() ?? PortName;
            if (doc.RootElement.TryGetProperty("baudRate", out var b) && b.TryGetInt32(out var baud))
                BaudRate = baud;
        }
        catch { /* corrupt settings file — keep defaults */ }
    }

    private void SaveSettings()
    {
        if (_settingsPath is null) return;
        try { File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new { portName = PortName, baudRate = BaudRate })); }
        catch { /* non-fatal */ }
    }
}
