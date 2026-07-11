using CommunityToolkit.Mvvm.ComponentModel;
using SerialForge.Core;

namespace SerialForge.App.ViewModels;

// One editable row in the auto-generated command form. Mirrors a single
// PayloadFieldDef: its Name/Codec are fixed, only Value is user-editable.
public partial class FieldEditorViewModel : ViewModelBase
{
    [ObservableProperty] private string _value = "";
    [ObservableProperty] private string? _validationMessage;

    public string Name { get; }
    public string Codec { get; }
    public bool IsReadOnly { get; }

    public FieldEditorViewModel(string name, string codec, string? defaultValue, bool isReadOnly)
    {
        Name = name;
        Codec = codec;
        IsReadOnly = isReadOnly;
        _value = defaultValue ?? "";
        Validate();
    }

    // Re-validate on each edit so the inline hint tracks the current value.
    partial void OnValueChanged(string value) => Validate();

    private void Validate()
    {
        if (IsReadOnly || !Enum.TryParse<CodecType>(Codec, out var codec))
        {
            ValidationMessage = null;
            return;
        }
        ValidationMessage = codec switch
        {
            CodecType.U8 => ValidateRange(Value, 0xFF, "0–255"),
            CodecType.U16 => ValidateRange(Value, 0xFFFF, "0–65535"),
            CodecType.U32 => ValidateRange(Value, 0xFFFFFFFFu, "0–4294967295"),
            _ => null
        };
    }

    private static string? ValidateRange(string text, ulong max, string range)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;   // empty: default/no value yet
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
