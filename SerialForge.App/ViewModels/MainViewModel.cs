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

    public MainViewModel() : this(new ProtocolCatalog().LoadFirst()) { }

    public MainViewModel(ProtocolDefinition? def)
    {
        Log = new LogViewModel();
        Connection = new ConnectionViewModel();
        _engine = def is null ? null! : new ProtocolEngine(def);
        _dispatcher = new FrameDispatcher(_engine, a => UiDispatcher.Marshal(a));
        _dispatcher.FrameDecoded += (_, f) => UiDispatcher.Marshal(() => Log.AddRx(f));

        void Send(byte[] frame)
        {
            UiDispatcher.Marshal(() => Log.AddTx(frame));
            var t = Connection.Transport;
            if (t is null || !t.IsOpen) return;
            try { t.Write(frame); }
            catch (Exception ex) { UiDispatcher.Marshal(() => System.Diagnostics.Debug.WriteLine("write failed: " + ex.Message)); }
        }
        CommandPanel = new CommandPanelViewModel(_engine, Send);
        if (def is not null) CommandPanel.Load(def);

        Connection.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConnectionViewModel.IsConnected) && Connection.Transport is not null)
                Connection.Transport.BytesReceived += (_, bytes) => _dispatcher.OnBytes(bytes);
        };
    }
}
