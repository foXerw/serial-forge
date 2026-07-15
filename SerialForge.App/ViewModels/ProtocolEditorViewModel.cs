using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialForge.App.Services;
using SerialForge.Core;
using SerialForge.Core.Exceptions;
using SerialForge.Core.SegmentModel;
using SegLoader = SerialForge.Core.SegmentModel.ProtocolLoader;
using SegSaver = SerialForge.Core.SegmentModel.ProtocolSaver;

namespace SerialForge.App.ViewModels;

// Mutable draft of a protocol. Edits live here; Build() produces a validated
// immutable SegmentModel.ProtocolDefinition. Apply pushes it to the running
// engine via the injected callback; Open/SaveAs go through IDialogService.
public sealed partial class ProtocolEditorViewModel : ViewModelBase
{
    private readonly Action<ProtocolDefinition> _apply;
    private readonly IDialogService? _dialogs;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _version = "1.0";
    [ObservableProperty] private ByteOrder _defaultByteOrder = ByteOrder.Little;
    [ObservableProperty] private string _filePath = "";
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private int _frameTimeoutMs = 50;
    [ObservableProperty] private string _rawJson = "";
    [ObservableProperty] private bool _isDirty;

    public ObservableCollection<SegmentViewModel> Segments { get; } = new();
    public ObservableCollection<CommandEditorViewModel> Commands { get; } = new();

    public ICommand Apply { get; }
    public ICommand Open { get; }
    public ICommand SaveAs { get; }
    public ICommand AddSegment { get; }
    public ICommand RemoveSegment { get; }
    public ICommand MoveSegmentUp { get; }
    public ICommand MoveSegmentDown { get; }
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
        SaveAs = new RelayCommand(DoSaveAs);
        AddSegment = new RelayCommand(() => { Segments.Add(new SegmentViewModel()); IsDirty = true; });
        RemoveSegment = new RelayCommand<SegmentViewModel>(s => { if (s is not null) Segments.Remove(s); IsDirty = true; });
        MoveSegmentUp = new RelayCommand<SegmentViewModel>(MoveUp);
        MoveSegmentDown = new RelayCommand<SegmentViewModel>(MoveDown);
        AddCommand = new RelayCommand(() => { Commands.Add(new CommandEditorViewModel()); IsDirty = true; });
        RemoveCommand = new RelayCommand<CommandEditorViewModel>(c => { if (c is not null) Commands.Remove(c); IsDirty = true; });
        RefreshRaw = new RelayCommand(DoRefreshRaw);
        ApplyRaw = new RelayCommand(DoApplyRaw);
        Populate(initial);
    }

    public void Populate(ProtocolDefinition? def)
    {
        if (def is null) return;
        Name = def.Name; Version = def.Version; DefaultByteOrder = def.DefaultByteOrder;
        FrameTimeoutMs = def.FrameTimeoutMs;
        Segments.Clear();
        foreach (var s in def.Frame) Segments.Add(new SegmentViewModel(s));
        Commands.Clear();
        foreach (var c in def.Commands) Commands.Add(new CommandEditorViewModel(c));
        ErrorMessage = null;
        IsDirty = false;
        OnPropertyChanged(nameof(Segments));
        OnPropertyChanged(nameof(Commands));
    }

    // Builds without validation — used for raw-JSON display so an invalid draft
    // still serializes. Build() adds the Validate call.
    internal ProtocolDefinition BuildDraft()
    {
        var frame = Segments.Select(s => s.ToSegment()).ToArray();
        var commands = Commands.Select(c => c.ToDef()).ToArray();
        return new ProtocolDefinition(Name, Version, DefaultByteOrder, frame, commands, FrameTimeoutMs);
    }

    public ProtocolDefinition Build()
    {
        var def = BuildDraft();
        SegLoader.Validate(def);
        return def;
    }

    private void MoveUp(SegmentViewModel? s)
    {
        if (s is null) return;
        int i = Segments.IndexOf(s);
        if (i > 0) { Segments.RemoveAt(i); Segments.Insert(i - 1, s); }
        IsDirty = true;
    }
    private void MoveDown(SegmentViewModel? s)
    {
        if (s is null) return;
        int i = Segments.IndexOf(s);
        if (i >= 0 && i < Segments.Count - 1) { Segments.RemoveAt(i); Segments.Insert(i + 1, s); }
        IsDirty = true;
    }

    private void DoRefreshRaw()
    {
        try { RawJson = SegSaver.ToJson(BuildDraft()); }
        catch (Exception ex) { RawJson = "<序列化失败：" + ex.Message + ">"; }
    }
    private void DoApplyRaw()
    {
        try { Populate(SegLoader.Load(RawJson)); }
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
        try { Populate(SegLoader.LoadFile(path)); FilePath = path; }
        catch (Exception ex) { ErrorMessage = "打开失败：" + ex.Message; }
    }

    private void DoSaveAs()
    {
        var path = _dialogs?.PickSaveJsonPath();
        if (path is null) return;
        try { SegSaver.ToFile(Build(), path); FilePath = path; ErrorMessage = null; }
        catch (Exception ex) { ErrorMessage = "保存失败：" + ex.Message; }
    }
}
