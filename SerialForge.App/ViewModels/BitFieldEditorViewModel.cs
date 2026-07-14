using CommunityToolkit.Mvvm.ComponentModel;
using SerialForge.Core.Models;

namespace SerialForge.App.ViewModels;

// Edits one bit sub-field. Enum typed as "0:idle,1:run" (colon-separated pairs).
// IsLength marks this child as the auto-computed length carrier; only meaningful
// (CanSetIsLength) when the owning field is a Computed length bitfield.
public sealed partial class BitFieldEditorViewModel : ViewModelBase
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private int _offset;
    [ObservableProperty] private int _width = 1;
    [ObservableProperty] private string _default = "";
    [ObservableProperty] private string _enum = "";
    [ObservableProperty] private bool _isLength;
    [ObservableProperty] private bool _canSetIsLength;

    public BitFieldEditorViewModel() { }
    public BitFieldEditorViewModel(BitFieldDef d)
    {
        _name = d.Name; _offset = d.Offset; _width = d.Width; _default = d.Default ?? "";
        _enum = d.Enum is null ? "" : string.Join(", ", d.Enum.Select(kv => $"{kv.Key}:{kv.Value}"));
        _isLength = d.IsLength;
    }

    public BitFieldDef ToDef()
        => new(Name, Offset, Width, ParseEnum(Enum), Default == "" ? null : Default, IsLength);

    private static Dictionary<string, string>? ParseEnum(string s)
    {
        var map = new Dictionary<string, string>();
        foreach (var pair in s.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = pair.Split(new[] { ':' }, 2);
            if (kv.Length == 2) map[kv[0].Trim()] = kv[1].Trim();
        }
        return map.Count == 0 ? null : map;
    }
}
