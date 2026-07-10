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

    private readonly ProtocolEngine _engine;
    private readonly FrameDispatcher _dispatcher;
    private ITransport? _subscribedTransport;
    private readonly EventHandler<byte[]> _onBytes;

    public MainViewModel() : this(new ProtocolCatalog().LoadFirst()) { }

    public MainViewModel(ProtocolDefinition? def)
    {
        Log = new LogViewModel();
        Connection = new ConnectionViewModel();
        _engine = def is null ? null! : new ProtocolEngine(def);
        _dispatcher = new FrameDispatcher(_engine, a => UiDispatcher.Marshal(a));
        _dispatcher.FrameDecoded += (_, f) => UiDispatcher.Marshal(() => Log.AddRx(f));

        // Shared handler bound once so += / -= use the same delegate instance,
        // letting us unsubscribe the previous transport before resubscribing
        // (prevents stacking across reconnects).
        _onBytes = (_, bytes) => _dispatcher.OnBytes(bytes);

        void Send(byte[] frame)
        {
            // Decode the just-encoded frame so the TX log shows the field breakdown
            // (acceptance #3), mirroring the RX path.
            UiDispatcher.Marshal(() =>
            {
                if (_engine is not null)
                    Log.AddTx(frame, _engine.Decode(frame));
                else
                    Log.AddTx(frame);
            });
            var t = Connection.Transport;
            if (t is null || !t.IsOpen) return;
            try { t.Write(frame); }
            catch (Exception ex) { UiDispatcher.Marshal(() => System.Diagnostics.Debug.WriteLine("write failed: " + ex.Message)); }
        }
        CommandPanel = new CommandPanelViewModel(_engine, Send);
        if (def is not null) CommandPanel.Load(def);

        // Subscribe on Transport changes (not IsConnected): reconnecting while
        // already connected replaces the transport without toggling IsConnected,
        // so only the Transport change reliably signals a new live instance.
        Connection.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ConnectionViewModel.Transport)) return;
            if (_subscribedTransport is not null)
                _subscribedTransport.BytesReceived -= _onBytes;
            _subscribedTransport = Connection.Transport;
            if (_subscribedTransport is not null)
                _subscribedTransport.BytesReceived += _onBytes;
        };
    }
}
