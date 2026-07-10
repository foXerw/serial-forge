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
    private readonly ProtocolEngine _engine;
    private readonly Action<byte[]> _send;
    private ProtocolDefinition? _def;

    public ObservableCollection<CommandDef> Commands { get; } = new();
    public ObservableCollection<FieldEditorViewModel> Fields { get; } = new();

    [ObservableProperty] private CommandDef? _selectedCommand;

    public ICommand Send { get; }

    public CommandPanelViewModel(ProtocolEngine engine, Action<byte[]> send) =>
        (_engine, _send, Send) = (engine, send, new RelayCommand(DoSend));

    public void Load(ProtocolDefinition def)
    {
        _def = def;
        Commands.Clear();
        foreach (var c in def.Commands) Commands.Add(c);
        SelectedCommand = Commands.FirstOrDefault();
    }

    // Source generator emits the call site for this when SelectedCommand changes.
    partial void OnSelectedCommandChanged(CommandDef? value)
    {
        Fields.Clear();
        if (value is null || _def is null) return;
        foreach (var pf in value.PayloadFields)
            Fields.Add(new FieldEditorViewModel(pf.Name, pf.Codec.ToString(), pf.Default, isReadOnly: false));
    }

    private void DoSend()
    {
        if (_def is null || SelectedCommand is null) return;
        var inst = new CommandInstance { Command = SelectedCommand };
        foreach (var fe in Fields)
        {
            var pf = SelectedCommand.PayloadFields.First(p => p.Name == fe.Name);
            inst.PayloadValues[fe.Name] = ParseValue(fe.Value, pf.Codec);
        }
        try
        {
            var frame = _engine.Encode(inst);
            _send(frame);
        }
        catch (Exception ex)
        {
            // Surface encode errors to the log (wired in Task 17). For now, no-op on failure.
            System.Diagnostics.Debug.WriteLine("encode failed: " + ex.Message);
        }
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
