using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialForge.Core;
using SerialForge.Core.Engine;
using SerialForge.Core.Models;

namespace SerialForge.App.ViewModels;

// Auto-generates an editable form from the selected command's PayloadFields,
// then builds a CommandInstance, encodes it via ProtocolEngine, and hands the
// framed bytes to the injected sender (wired to the transport in Task 17).
public partial class CommandPanelViewModel : ViewModelBase
{
    private ProtocolEngine _engine;
    private readonly Action<byte[]> _send;
    private readonly Action<string>? _onError;
    private readonly RelayCommand _sendCommand;
    private ProtocolDefinition? _def;
    // Per-command field values, so switching away and back doesn't lose edits.
    private readonly Dictionary<string, Dictionary<string, string>> _valueCache = new();
    private string? _currentCommandName;
    private bool _suppressSnapshot;   // set during RestoreSession to avoid stale overwrite

    public ObservableCollection<CommandDef> Commands { get; } = new();
    public ObservableCollection<FieldEditorViewModel> Fields { get; } = new();

    [ObservableProperty] private CommandDef? _selectedCommand;

    public ICommand Send => _sendCommand;

    public CommandPanelViewModel(ProtocolEngine engine, Action<byte[]> send, Action<string>? onError = null)
    {
        _engine = engine;
        _send = send;
        _onError = onError;
        _sendCommand = new RelayCommand(DoSend, () => SelectedCommand is not null);
    }

    public void Load(ProtocolDefinition def)
    {
        _def = def;
        _valueCache.Clear();          // don't leak values across protocols (hot-swap)
        _currentCommandName = null;
        Commands.Clear();
        foreach (var c in def.Commands) Commands.Add(c);
        SelectedCommand = Commands.FirstOrDefault();
    }

    // Session: snapshot ALL command field values (cache + the currently-shown
    // command's Fields) so they can be serialized and restored later.
    public Dictionary<string, Dictionary<string, string>> SnapshotSession()
    {
        var snap = new Dictionary<string, Dictionary<string, string>>(_valueCache);
        if (_currentCommandName is not null)
            snap[_currentCommandName] = Fields.ToDictionary(f => f.Name, f => f.Value);
        return snap;
    }

    public string? SelectedCommandName => SelectedCommand?.Name;

    // Restore a previously snapshotted session: load the per-command values, then
    // re-select the command (which rebuilds its fields from the restored cache).
    public void RestoreSession(Dictionary<string, Dictionary<string, string>> values, string? selectedCommandName)
    {
        _suppressSnapshot = true;
        try
        {
            _valueCache.Clear();
            foreach (var kv in values) _valueCache[kv.Key] = new Dictionary<string, string>(kv.Value);
            SelectedCommand = null;
            if (selectedCommandName is not null)
            {
                var cmd = Commands.FirstOrDefault(c => c.Name == selectedCommandName);
                if (cmd is not null) SelectedCommand = cmd;
            }
        }
        finally { _suppressSnapshot = false; }
    }

    // Hot-swap: rebind to a rebuilt engine and reload commands from the new def.
    // Load() refreshes Commands + SelectedCommand and notifies Send CanExecute.
    public void Reload(ProtocolEngine engine, ProtocolDefinition def)
    {
        _engine = engine;
        Load(def);
    }

    // Source generator emits the call site for this when SelectedCommand changes.
    partial void OnSelectedCommandChanged(CommandDef? value)
    {
        _sendCommand.NotifyCanExecuteChanged();
        // Snapshot the outgoing command's field values before rebuilding, so
        // switching away and back doesn't lose what the user typed.
        if (!_suppressSnapshot && _currentCommandName is not null)
            _valueCache[_currentCommandName] = Fields.ToDictionary(f => f.Name, f => f.Value);
        Fields.Clear();
        if (value is null || _def is null) { _currentCommandName = null; return; }
        _currentCommandName = value.Name;
        foreach (var pf in value.PayloadFields)
        {
            var initial = (_valueCache.TryGetValue(value.Name, out var c) && c.TryGetValue(pf.Name, out var v)) ? v : pf.Default;
            Fields.Add(new FieldEditorViewModel(pf.Name, pf.Codec.ToString(), initial, isReadOnly: false));
        }
    }

    private void DoSend()
    {
        if (_def is null || SelectedCommand is null) return;
        byte[] frame;
        try
        {
            var inst = new CommandInstance { Command = SelectedCommand };
            foreach (var fe in Fields)
            {
                var pf = SelectedCommand.PayloadFields.First(p => p.Name == fe.Name);
                inst.PayloadValues[fe.Name] = ParseValue(fe.Value, pf.Codec);
            }
            frame = _engine.Encode(inst);
        }
        catch (Exception ex)
        {
            // Bad user input (parse/overflow) or encode failure: surface to the log,
            // send nothing over the wire (spec §8).
            _onError?.Invoke("编码失败：" + ex.Message);
            return;
        }
        _send(frame);
    }

    // Numeric fields parse to ulong (UIntCodec accepts ulong directly); hex is
    // honoured via the 0x prefix. bytes/string/raw pass through as text for the
    // engine's BytesCodec.ParseHex path.
    private static object ParseValue(string text, CodecType codec) => codec switch
    {
        CodecType.U8 or CodecType.U16 or CodecType.U32 when text.StartsWith("0x")
            => Convert.ToUInt64(text.Replace("0x", ""), 16),
        CodecType.U8 or CodecType.U16 or CodecType.U32 when int.TryParse(text, out var d) => (ulong)d,
        _ => text
    };
}
