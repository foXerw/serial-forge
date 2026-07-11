using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialForge.App.Services;
using SerialForge.Core.Engine;
using SerialForge.Core.Models;
using SerialForge.Transport;

namespace SerialForge.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ConnectionViewModel Connection { get; }
    public CommandPanelViewModel CommandPanel { get; }
    public LogViewModel Log { get; }
    public RawSendViewModel RawSend { get; }

    private ProtocolEngine _engine;
    private FrameDispatcher _dispatcher;
    private ProtocolDefinition? _currentDef;
    private readonly IDialogService? _dialogs;
    private ITransport? _subscribedTransport;
    private EventHandler<byte[]> _onBytes;

    public ICommand OpenEditor { get; }
    public ICommand ShowHelp { get; }
    public ICommand OpenUpgrade { get; }

    public MainViewModel() : this(new ProtocolCatalog().LoadFirst(), new DialogService()) { }

    public MainViewModel(ProtocolDefinition? def) : this(def, null) { }

    public MainViewModel(ProtocolDefinition? def, IDialogService? dialogs)
    {
        _dialogs = dialogs;
        Log = new LogViewModel();
        Connection = new ConnectionViewModel();
        _currentDef = def;
        _engine = def is null ? null! : new ProtocolEngine(def);
        _dispatcher = new FrameDispatcher(_engine, a => UiDispatcher.Marshal(a));
        _dispatcher.FrameDecoded += (_, f) => UiDispatcher.Marshal(() => Log.AddRx(f));

        _onBytes = (_, bytes) => _dispatcher.OnBytes(bytes);

        void Send(byte[] frame)
        {
            UiDispatcher.Marshal(() =>
            {
                if (_engine is not null) Log.AddTx(frame, _engine.Decode(frame));
                else Log.AddTx(frame);
            });
            var t = Connection.Transport;
            if (t is null || !t.IsOpen) return;
            try { t.Write(frame); }
            catch (Exception ex) { UiDispatcher.Marshal(() => System.Diagnostics.Debug.WriteLine("write failed: " + ex.Message)); }
        }
        void SendRaw(byte[] bytes)
        {
            UiDispatcher.Marshal(() => Log.AddTx(bytes));   // raw TX, no protocol decode
            var t = Connection.Transport;
            if (t is null || !t.IsOpen) return;
            try { t.Write(bytes); }
            catch (Exception ex) { UiDispatcher.Marshal(() => System.Diagnostics.Debug.WriteLine("raw write failed: " + ex.Message)); }
        }
        CommandPanel = new CommandPanelViewModel(_engine, Send, msg => Log.AddError(msg));
        RawSend = new RawSendViewModel(SendRaw, msg => Log.AddError(msg));
        if (def is not null) CommandPanel.Load(def);

        Connection.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ConnectionViewModel.Transport)) return;
            if (_subscribedTransport is not null) _subscribedTransport.BytesReceived -= _onBytes;
            _subscribedTransport = Connection.Transport;
            if (_subscribedTransport is not null) _subscribedTransport.BytesReceived += _onBytes;
        };

        OpenEditor = new RelayCommand(() =>
        {
            if (_dialogs is null || _currentDef is null) return;
            _dialogs.ShowEditor(new ProtocolEditorViewModel(_currentDef, ApplyProtocol, _dialogs));
        });
        ShowHelp = new RelayCommand(() => _dialogs?.ShowHelp());
        OpenUpgrade = new RelayCommand(() =>
        {
            if (_dialogs is null) return;
            var transport = Connection.Transport;
            if (transport is null || !transport.IsOpen) { Log.AddError("固件升级需要先连接串口"); return; }
            var runner = new UpgradeRunner(_engine, _dispatcher, transport,
                frame => Log.AddTx(frame, _engine.Decode(frame)));
            _dialogs.ShowUpgrade(new UpgradeViewModel(_dialogs, runner));
        });
    }

    // Hot-swap the running protocol: new engine + dispatcher, re-bind transport
    // subscription, reload command panel. Log is preserved.
    public void ApplyProtocol(ProtocolDefinition def)
    {
        _currentDef = def;
        _engine = new ProtocolEngine(def);
        _dispatcher = new FrameDispatcher(_engine, a => UiDispatcher.Marshal(a));
        _dispatcher.FrameDecoded += (_, f) => UiDispatcher.Marshal(() => Log.AddRx(f));

        if (_subscribedTransport is not null)
        {
            _subscribedTransport.BytesReceived -= _onBytes;
            _onBytes = (_, bytes) => _dispatcher.OnBytes(bytes);
            _subscribedTransport.BytesReceived += _onBytes;
        }
        CommandPanel.Reload(_engine, def);
    }
}
