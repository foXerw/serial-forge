using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialForge.Core.Engine;
using SerialForge.Core.Models;
using SerialForge.Core.SegmentModel;

namespace SerialForge.App.ViewModels;

// Builds an editable form from the frame's Value segments (plus the selected
// command's payload sub-template, if any), merges in the command's preset
// Values, encodes via FrameEngine, and hands the bytes to the injected sender.
public partial class CommandPanelViewModel : ViewModelBase
{
    private FrameEngine _engine;
    private ProtocolDefinition? _def;
    private readonly Action<byte[]> _send;
    private readonly Action<string>? _onError;
    private readonly RelayCommand _sendCommand;
    private readonly Dictionary<string, Dictionary<string, string>> _valueCache = new();
    private string? _currentCommandName;
    private bool _suppressSnapshot;

    public ObservableCollection<CommandDef> Commands { get; } = new();
    public ObservableCollection<FieldEditorViewModel> Fields { get; } = new();

    [ObservableProperty] private CommandDef? _selectedCommand;

    public ICommand Send => _sendCommand;

    public CommandPanelViewModel(FrameEngine engine, ProtocolDefinition def, Action<byte[]> send, Action<string>? onError = null)
    {
        _engine = engine;
        _def = def;
        _send = send;
        _onError = onError;
        _sendCommand = new RelayCommand(DoSend, () => SelectedCommand is not null);
        Load(def);
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

    // Hot-swap: rebind to a rebuilt engine and reload commands from the new def.
    public void Reload(FrameEngine engine, ProtocolDefinition def)
    {
        _engine = engine;
        Load(def);
    }

    public string? SelectedCommandName => SelectedCommand?.Name;

    // Session snapshot/restore of ALL command field values.
    public Dictionary<string, Dictionary<string, string>> SnapshotSession()
    {
        var snap = new Dictionary<string, Dictionary<string, string>>(_valueCache);
        if (_currentCommandName is not null)
            snap[_currentCommandName] = Fields.ToDictionary(f => f.Name, f => f.Value);
        return snap;
    }

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

    partial void OnSelectedCommandChanged(CommandDef? value)
    {
        _sendCommand.NotifyCanExecuteChanged();
        if (!_suppressSnapshot && _currentCommandName is not null)
            _valueCache[_currentCommandName] = Fields.ToDictionary(f => f.Name, f => f.Value);
        Fields.Clear();
        if (value is null || _def is null) { _currentCommandName = null; return; }
        _currentCommandName = value.Name;
        // Editable rows: Value segments NOT preset by the command (those are fixed),
        // with the variable payload segment expanded via the command's payload
        // sub-template when present.
        foreach (var seg in _def.Frame)
        {
            if (seg.Role != SegmentRole.Value) continue;
            if (value.Values.ContainsKey(seg.Name)) continue;   // preset by command (e.g. cmd)
            if (seg.Width is null)
            {
                if (value.Payload is { Length: > 0 } tmpl)
                {
                    foreach (var ps in tmpl) AddField(ps.Name, ps.Width, ps.Default, value);
                }
                else
                {
                    AddField(seg.Name, null, seg.Default, value);   // free-form hex payload
                }
            }
            else
            {
                AddField(seg.Name, seg.Width, seg.Default, value);
            }
        }
    }

    private void AddField(string name, int? width, string? defaultVal, CommandDef cmd)
    {
        var initial = (_valueCache.TryGetValue(cmd.Name, out var c) && c.TryGetValue(name, out var v))
            ? v : (defaultVal ?? "");
        ulong? max = width is int w and >= 1 ? (ulong)((1L << w) - 1) : null;
        Fields.Add(new FieldEditorViewModel(name, initial, isReadOnly: false, maxValue: max));
    }

    private void DoSend()
    {
        if (_def is null || SelectedCommand is null) return;
        byte[] frame;
        try
        {
            var values = new Dictionary<string, object>();
            foreach (var kv in SelectedCommand.Values) values[kv.Key] = kv.Value;   // command presets (e.g. cmd)
            foreach (var fe in Fields)
            {
                if (fe.MaxValue is null) values[fe.Name] = fe.Value;        // payload: pass hex string through
                else values[fe.Name] = ParseInt(fe.Value);
            }
            frame = _engine.Pack(values, SelectedCommand.Payload);
        }
        catch (Exception ex)
        {
            _onError?.Invoke("编码失败：" + ex.Message);
            return;
        }
        _send(frame);
    }

    private static object ParseInt(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0UL;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt64(text[2..], 16);
        return int.TryParse(text, out var d) ? (object)(ulong)d : text;
    }
}
