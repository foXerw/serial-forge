using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialForge.App.Services;
using SerialForge.Core;
using SerialForge.Core.Engine;
using SerialForge.Core.Exceptions;
using SerialForge.Core.Models;

namespace SerialForge.App.ViewModels;

// Mutable draft of a protocol. Edits live here; Build() produces a validated
// immutable ProtocolDefinition. Apply pushes the built def to the running engine
// via the injected callback; Open/SaveAs go through IDialogService.
public sealed partial class ProtocolEditorViewModel : ViewModelBase
{
    private readonly Action<ProtocolDefinition> _apply;
    private readonly IDialogService? _dialogs;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _version = "1.0";
    [ObservableProperty] private ByteOrder _defaultByteOrder = ByteOrder.Little;
    [ObservableProperty] private string _filePath = "";
    [ObservableProperty] private string? _errorMessage;

    // Framing draft (flat — bound directly in XAML)
    [ObservableProperty] private FramingMode _framingMode = FramingMode.LengthPrefix;
    [ObservableProperty] private string _preamble = "0xAA 0x55";
    [ObservableProperty] private string _lengthField = "len";
    [ObservableProperty] private int _frameTimeoutMs = 50;

    public ObservableCollection<LayoutFieldViewModel> LayoutFields { get; } = new();
    public ObservableCollection<CommandEditorViewModel> Commands { get; } = new();

    public ICommand Apply { get; }
    public ICommand Open { get; }
    public ICommand SaveAs { get; }

    public ProtocolEditorViewModel(ProtocolDefinition? initial, Action<ProtocolDefinition> apply, IDialogService? dialogs)
    {
        _apply = apply;
        _dialogs = dialogs;
        Apply = new RelayCommand(DoApply);
        Open = new RelayCommand(DoOpen);
        SaveAs = new RelayCommand(DoSaveAs, () => true);
        Populate(initial);
    }

    public void Populate(ProtocolDefinition? def)
    {
        if (def is null) return;
        Name = def.Name; Version = def.Version; DefaultByteOrder = def.DefaultByteOrder;
        FramingMode = def.Framing.Mode;
        Preamble = def.Framing.Preamble is null ? "" : string.Join(" ", def.Framing.Preamble);
        LengthField = def.Framing.LengthField ?? "";
        FrameTimeoutMs = def.Framing.FrameTimeoutMs;
        LayoutFields.Clear();
        foreach (var f in def.Layout) LayoutFields.Add(new LayoutFieldViewModel(f));
        Commands.Clear();
        foreach (var c in def.Commands) Commands.Add(new CommandEditorViewModel(c));
        ErrorMessage = null;
        OnPropertyChanged(nameof(LayoutFields));
        OnPropertyChanged(nameof(Commands));
    }

    public ProtocolDefinition Build()
    {
        var framing = new FramingRule(
            FramingMode,
            SplitHex(Preamble),
            string.IsNullOrWhiteSpace(LengthField) ? null : LengthField.Trim(),
            FrameTimeoutMs, null, null);
        var layout = LayoutFields.Select(f => f.ToFieldDef(DefaultByteOrder)).ToArray();
        var commands = Commands.Select(c => c.ToDef()).ToArray();
        var def = new ProtocolDefinition(Name, Version, DefaultByteOrder, framing, layout, commands);
        ProtocolLoader.Validate(def);   // throws ProtocolException if invalid
        return def;
    }

    private void DoApply()
    {
        try { _apply(Build()); ErrorMessage = null; }
        catch (ProtocolException ex) { ErrorMessage = ex.Message; }
    }

    private void DoOpen()
    {
        var path = _dialogs?.PickOpenJsonPath();
        if (path is null) return;
        try { Populate(ProtocolLoader.LoadFile(path)); FilePath = path; }
        catch (Exception ex) { ErrorMessage = "打开失败：" + ex.Message; }
    }

    private void DoSaveAs()
    {
        var path = _dialogs?.PickSaveJsonPath();
        if (path is null) return;
        try { ProtocolSaver.ToFile(Build(), path); FilePath = path; ErrorMessage = null; }
        catch (Exception ex) { ErrorMessage = "保存失败：" + ex.Message; }
    }

    private static string[]? SplitHex(string s)
    {
        var arr = s.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return arr.Length == 0 ? null : arr;
    }
}
