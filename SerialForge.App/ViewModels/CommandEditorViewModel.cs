using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SerialForge.Core.Models;

namespace SerialForge.App.ViewModels;

public sealed partial class FixEntry : ViewModelBase
{
    [ObservableProperty] private string _field = "";
    [ObservableProperty] private string _value = "";
    public FixEntry() { }
    public FixEntry(string field, string value) { _field = field; _value = value; }
}

public sealed partial class CommandEditorViewModel : ViewModelBase
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _title = "";
    public ObservableCollection<FixEntry> Fix { get; } = new();
    public ObservableCollection<PayloadFieldViewModel> PayloadFields { get; } = new();

    public CommandEditorViewModel() { }
    public CommandEditorViewModel(CommandDef c)
    {
        _name = c.Name; _title = c.Title;
        foreach (var kv in c.Fix) Fix.Add(new FixEntry(kv.Key, kv.Value));
        foreach (var p in c.PayloadFields) PayloadFields.Add(new PayloadFieldViewModel(p));
    }

    public CommandDef ToDef() => new(
        Name,
        string.IsNullOrWhiteSpace(Title) ? Name : Title,
        Fix.Where(f => !string.IsNullOrWhiteSpace(f.Field)).ToDictionary(f => f.Field, f => f.Value),
        PayloadFields.Select(p => p.ToDef()).ToArray());
}
