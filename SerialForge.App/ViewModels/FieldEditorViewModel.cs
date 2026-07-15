using CommunityToolkit.Mvvm.ComponentModel;

namespace SerialForge.App.ViewModels;

// One editable row in the command form. Width-driven: a maxValue (derived from
// the segment's bit width) bounds numeric input; null maxValue means free-form
// hex (the variable payload segment, typed as a hex string).
public partial class FieldEditorViewModel : ViewModelBase
{
    [ObservableProperty] private string _value = "";
    [ObservableProperty] private string? _validationMessage;

    public string Name { get; }
    public bool IsReadOnly { get; }
    public ulong? MaxValue { get; }       // null => free-form hex (payload)
    private readonly ulong? _maxValue;

    public FieldEditorViewModel(string name, string? defaultValue, bool isReadOnly, ulong? maxValue = null)
    {
        Name = name;
        IsReadOnly = isReadOnly;
        MaxValue = maxValue;
        _maxValue = maxValue;
        _value = defaultValue ?? "";
        Validate();
    }

    // Re-validate on each edit so the inline hint tracks the current value.
    partial void OnValueChanged(string value) => Validate();

    private void Validate()
    {
        if (IsReadOnly || string.IsNullOrWhiteSpace(Value)) { ValidationMessage = null; return; }
        if (_maxValue is { } max)
        {
            ValidationMessage = ValidateRange(Value, max, $"0–{max}");
            return;
        }
        ValidationMessage = null;   // free-form hex payload: no range check
    }

    private static string? ValidateRange(string text, ulong max, string range)
    {
        try
        {
            ulong v = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToUInt64(text[2..], 16)
                : ulong.Parse(text);
            return v > max ? $"超出范围（{range}）" : null;
        }
        catch
        {
            return $"需为十进制或 0x 十六进制（{range}）";
        }
    }
}
