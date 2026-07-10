using CommunityToolkit.Mvvm.ComponentModel;

namespace SerialForge.App.ViewModels;

// One editable row in the auto-generated command form. Mirrors a single
// PayloadFieldDef: its Name/Codec are fixed, only Value is user-editable.
public partial class FieldEditorViewModel : ViewModelBase
{
    [ObservableProperty] private string _value = "";

    public string Name { get; }
    public string Codec { get; }
    public bool IsReadOnly { get; }

    public FieldEditorViewModel(string name, string codec, string? defaultValue, bool isReadOnly)
    {
        Name = name;
        Codec = codec;
        IsReadOnly = isReadOnly;
        _value = defaultValue ?? "";
    }
}
