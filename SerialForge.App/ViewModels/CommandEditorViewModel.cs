using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialForge.Core.Models;
using SerialForge.Core.SegmentModel;

namespace SerialForge.App.ViewModels;

public sealed partial class FixEntry : ViewModelBase
{
    [ObservableProperty] private string _field = "";
    [ObservableProperty] private string _value = "";
    public FixEntry() { }
    public FixEntry(string field, string value) { _field = field; _value = value; }
}

// Edits a command's preset Values (field=value pairs). The optional Payload
// sub-template is preserved across load/save for round-trip but not GUI-editable
// (edit it via the raw-JSON tab).
public sealed partial class CommandEditorViewModel : ViewModelBase
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _title = "";
    public ObservableCollection<FixEntry> Values { get; } = new();
    public Segment[]? Payload { get; set; }

    public ICommand AddValue { get; }
    public ICommand RemoveValue { get; }

    public CommandEditorViewModel()
    {
        AddValue = new RelayCommand(() => Values.Add(new FixEntry()));
        RemoveValue = new RelayCommand<FixEntry>(e => Values.Remove(e!));
    }
    public CommandEditorViewModel(CommandDef c) : this()
    {
        _name = c.Name; _title = c.Title;
        foreach (var kv in c.Values) Values.Add(new FixEntry(kv.Key, kv.Value));
        Payload = c.Payload;
    }

    public CommandDef ToDef() => new(
        Name,
        string.IsNullOrWhiteSpace(Title) ? Name : Title,
        Values.Where(v => !string.IsNullOrWhiteSpace(v.Field)).ToDictionary(v => v.Field, v => v.Value),
        Payload);
}
