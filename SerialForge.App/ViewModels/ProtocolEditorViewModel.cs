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
    [ObservableProperty] private string _rawJson = "";
    [ObservableProperty] private bool _isDirty;

    public ObservableCollection<LayoutFieldViewModel> LayoutFields { get; } = new();
    public ObservableCollection<CommandEditorViewModel> Commands { get; } = new();

    public ICommand Apply { get; }
    public ICommand Open { get; }
    public ICommand SaveAs { get; }
    public ICommand AddLayoutField { get; }
    public ICommand RemoveLayoutField { get; }
    public ICommand MoveLayoutFieldUp { get; }
    public ICommand MoveLayoutFieldDown { get; }
    public ICommand AddCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand RefreshRaw { get; }
    public ICommand ApplyRaw { get; }

    public ProtocolEditorViewModel(ProtocolDefinition? initial, Action<ProtocolDefinition> apply, IDialogService? dialogs)
    {
        _apply = apply;
        _dialogs = dialogs;
        Apply = new RelayCommand(DoApply);
        Open = new RelayCommand(DoOpen);
        SaveAs = new RelayCommand(DoSaveAs, () => true);
        AddLayoutField = new RelayCommand(DoAddLayoutField);
        RemoveLayoutField = new RelayCommand<LayoutFieldViewModel>(DoRemoveLayoutField);
        MoveLayoutFieldUp = new RelayCommand<LayoutFieldViewModel>(DoMoveLayoutFieldUp);
        MoveLayoutFieldDown = new RelayCommand<LayoutFieldViewModel>(DoMoveLayoutFieldDown);
        AddCommand = new RelayCommand(DoAddCommand);
        RemoveCommand = new RelayCommand<CommandEditorViewModel>(DoRemoveCommand);
        RefreshRaw = new RelayCommand(DoRefreshRaw);
        ApplyRaw = new RelayCommand(DoApplyRaw);
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
        IsDirty = false;
        OnPropertyChanged(nameof(LayoutFields));
        OnPropertyChanged(nameof(Commands));
    }

    // Constructs the record WITHOUT validation — used for raw-JSON display so an
    // invalid draft still serializes. Build() adds the Validate call.
    internal ProtocolDefinition BuildDraft()
    {
        var framing = new FramingRule(
            FramingMode,
            SplitHex(Preamble),
            string.IsNullOrWhiteSpace(LengthField) ? null : LengthField.Trim(),
            FrameTimeoutMs, null, null);
        var layout = LayoutFields.Select(f => f.ToFieldDef(DefaultByteOrder)).ToArray();
        var commands = Commands.Select(c => c.ToDef()).ToArray();
        return new ProtocolDefinition(Name, Version, DefaultByteOrder, framing, layout, commands);
    }

    public ProtocolDefinition Build()
    {
        var def = BuildDraft();
        ProtocolLoader.Validate(def);   // throws ProtocolException if invalid
        return def;
    }

    private void DoAddLayoutField() { LayoutFields.Add(new LayoutFieldViewModel()); IsDirty = true; }
    private void DoRemoveLayoutField(LayoutFieldViewModel? f) { if (f is not null) LayoutFields.Remove(f); IsDirty = true; }
    private void DoMoveLayoutFieldUp(LayoutFieldViewModel? f)
    {
        if (f is null) return;
        int i = LayoutFields.IndexOf(f);
        if (i > 0) { LayoutFields.RemoveAt(i); LayoutFields.Insert(i - 1, f); }
        IsDirty = true;
    }
    private void DoMoveLayoutFieldDown(LayoutFieldViewModel? f)
    {
        if (f is null) return;
        int i = LayoutFields.IndexOf(f);
        if (i >= 0 && i < LayoutFields.Count - 1) { LayoutFields.RemoveAt(i); LayoutFields.Insert(i + 1, f); }
        IsDirty = true;
    }
    private void DoAddCommand() { Commands.Add(new CommandEditorViewModel()); IsDirty = true; }
    private void DoRemoveCommand(CommandEditorViewModel? c) { if (c is not null) Commands.Remove(c); IsDirty = true; }

    private void DoRefreshRaw()
    {
        try { RawJson = ProtocolSaver.ToJson(BuildDraft()); }
        catch (Exception ex) { RawJson = "<序列化失败：" + ex.Message + ">"; }
    }
    private void DoApplyRaw()
    {
        // Populate resets IsDirty=false (a JSON load is a clean state).
        try { Populate(ProtocolLoader.Load(RawJson)); }
        catch (Exception ex) { ErrorMessage = "JSON 解析失败：" + ex.Message; }
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
