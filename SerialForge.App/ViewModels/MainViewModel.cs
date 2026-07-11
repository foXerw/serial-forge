using System.IO;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;
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
    private readonly DispatcherTimer? _flushTimer;

    public ICommand OpenEditor { get; }
    public ICommand ShowHelp { get; }
    public ICommand OpenUpgrade { get; }
    public ICommand ExportLog { get; }
    public ICommand SaveSession { get; }
    public ICommand LoadSession { get; }

    public MainViewModel() : this(new ProtocolCatalog().LoadFirst(), new DialogService())
    {
        // Idle-flush timer: drives FrameDispatcher.Tick so Timeout/delimiter framing
        // (and stalled length-prefix reads) flush partial frames. Only the production
        // ctor starts it; test ctors are unaffected.
        _flushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _flushTimer.Tick += (_, _) => _dispatcher.Tick();
        _flushTimer.Start();
    }

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
        ExportLog = new RelayCommand(() =>
        {
            var path = _dialogs?.PickSaveLogPath();
            if (path is null) return;
            try { Log.Export(path); }
            catch (Exception ex) { Log.AddError("导出失败：" + ex.Message); }
        });
        SaveSession = new RelayCommand(() =>
        {
            var path = _dialogs?.PickSaveSessionPath();
            if (path is null) return;
            try
            {
                var session = new { selectedCommand = CommandPanel.SelectedCommandName, values = CommandPanel.SnapshotSession() };
                File.WriteAllText(path, JsonSerializer.Serialize(session));
            }
            catch (Exception ex) { Log.AddError("保存会话失败：" + ex.Message); }
        });
        LoadSession = new RelayCommand(() =>
        {
            var path = _dialogs?.PickOpenSessionPath();
            if (path is null) return;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var values = doc.RootElement.TryGetProperty("values", out var vEl)
                    ? vEl.Deserialize<Dictionary<string, Dictionary<string, string>>>() ?? new()
                    : new Dictionary<string, Dictionary<string, string>>();
                var selected = doc.RootElement.TryGetProperty("selectedCommand", out var sEl) && sEl.ValueKind == JsonValueKind.String
                    ? sEl.GetString() : null;
                CommandPanel.RestoreSession(values, selected);
            }
            catch (Exception ex) { Log.AddError("加载会话失败：" + ex.Message); }
        });
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
