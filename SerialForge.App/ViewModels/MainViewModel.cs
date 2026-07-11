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

    private ProtocolEngine _engine;
    private FrameDispatcher _dispatcher;
    private ProtocolDefinition? _currentDef;
    private readonly IDialogService? _dialogs;
    private ITransport? _subscribedTransport;
    private EventHandler<byte[]> _onBytes;

    public ICommand OpenEditor { get; }
    public ICommand ShowHelp { get; }

    // Production: load demo; dialogs wired in Task 9 (DialogService). Until then
    // null dialogs make OpenEditor/ShowHelp no-op so Task 6 compiles standalone.
    public MainViewModel() : this(new ProtocolCatalog().LoadFirst(), null) { }

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
        CommandPanel = new CommandPanelViewModel(_engine, Send, msg => Log.AddError(msg));
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
