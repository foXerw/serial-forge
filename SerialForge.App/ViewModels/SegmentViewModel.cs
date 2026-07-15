using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using SerialForge.Core;
using SerialForge.Core.Algorithms;
using SerialForge.Core.Models;
using SerialForge.Core.SegmentModel;

namespace SerialForge.App.ViewModels;

// Edits one segment. Fields apply per Role (the XAML shows/hides groups by Role).
// Replaces the legacy LayoutField/PayloadField/BitFieldEditor/ComputeEditor quartet.
public sealed partial class SegmentViewModel : ViewModelBase
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private SegmentRole _role = SegmentRole.Value;
    [ObservableProperty] private string _width = "";        // bits; "" => variable payload
    [ObservableProperty] private ByteOrder? _byteOrder;      // null = protocol default
    [ObservableProperty] private string _value = "";         // Fixed: hex bytes "AA 55"
    [ObservableProperty] private string _default = "";       // Value: integer/hex
    [ObservableProperty] private string _enum = "";          // Value: "0:idle,1:run"
    [ObservableProperty] private string _counts = "";        // Length: comma-separated names
    [ObservableProperty] private int _offset;                // Length
    [ObservableProperty] private string _algo = "";          // Checksum: crc16|crc32|sum8|xor8
    [ObservableProperty] private string _overFrom = "";      // Checksum
    [ObservableProperty] private string _overTo = "";        // Checksum
    [ObservableProperty] private string _poly = "";
    [ObservableProperty] private string _init = "";
    [ObservableProperty] private string _xorOut = "";
    [ObservableProperty] private bool _refIn;
    [ObservableProperty] private bool _refOut;

    public static readonly SegmentRole[] Roles = System.Enum.GetValues<SegmentRole>();
    public static readonly string[] Algos = { "crc16", "crc32", "sum8", "xor8" };
    public bool IsFixed => Role == SegmentRole.Fixed;
    public bool IsValue => Role == SegmentRole.Value;
    public bool IsLength => Role == SegmentRole.Length;
    public bool IsChecksum => Role == SegmentRole.Checksum;
    public bool IsCrc => Algo is "crc16" or "crc32";

    partial void OnRoleChanged(SegmentRole value)
    {
        OnPropertyChanged(nameof(IsFixed));
        OnPropertyChanged(nameof(IsValue));
        OnPropertyChanged(nameof(IsLength));
        OnPropertyChanged(nameof(IsChecksum));
    }

    public SegmentViewModel() { }
    public SegmentViewModel(Segment s)
    {
        _name = s.Name; _role = s.Role;
        _width = s.Width?.ToString() ?? "";
        _byteOrder = s.ByteOrder;
        _value = s.Value is null ? "" : string.Join(" ", s.Value);
        _default = s.Default ?? "";
        _enum = s.Enum is null ? "" : string.Join(", ", s.Enum.Select(kv => $"{kv.Key}:{kv.Value}"));
        _counts = s.Counts is null ? "" : string.Join(", ", s.Counts);
        _offset = s.Offset;
        _algo = s.Algo ?? "";
        _overFrom = s.OverFrom ?? "";
        _overTo = s.OverTo ?? "";
        _poly = GetRaw(s.Params, "poly");
        _init = GetRaw(s.Params, "init");
        _xorOut = GetRaw(s.Params, "xorOut");
        _refIn = s.Params.GetBool("refIn", false);
        _refOut = s.Params.GetBool("refOut", false);
    }

    public Segment ToSegment()
    {
        int? width = int.TryParse(Width, out var w) ? w : null;
        var value = Role == SegmentRole.Fixed ? SplitHex(Value) : null;
        var def = Role == SegmentRole.Value && !string.IsNullOrWhiteSpace(Default) ? Default : null;
        string? algo = Role == SegmentRole.Checksum && !string.IsNullOrWhiteSpace(Algo) ? Algo : null;
        string? overFrom = Role == SegmentRole.Checksum && !string.IsNullOrWhiteSpace(OverFrom) ? OverFrom.Trim() : null;
        string? overTo = Role == SegmentRole.Checksum && !string.IsNullOrWhiteSpace(OverTo) ? OverTo.Trim() : null;
        var counts = Role == SegmentRole.Length ? SplitNames(Counts) : null;
        var p = (Role == SegmentRole.Checksum && IsCrc) ? CrcParams() : null;
        return new Segment(Name, Role, width, ByteOrder, value, def, ParseEnum(Enum),
            counts, Offset, algo, overFrom, overTo, p);
    }

    private Dictionary<string, JsonElement>? CrcParams()
    {
        var d = new Dictionary<string, JsonElement>();
        AddHex(d, "poly", Poly);
        AddHex(d, "init", Init);
        AddHex(d, "xorOut", XorOut);
        d["refIn"] = JsonSerializer.SerializeToElement(RefIn);
        d["refOut"] = JsonSerializer.SerializeToElement(RefOut);
        return d;
    }
    private static void AddHex(Dictionary<string, JsonElement> d, string key, string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return;
        string digits = val.Trim().Replace("0x", "").Replace("0X", "");
        d[key] = JsonSerializer.SerializeToElement("0x" + digits.ToLowerInvariant());
    }

    private static string[]? SplitHex(string s)
    {
        var arr = s.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return arr.Length == 0 ? null : arr;
    }
    private static string[]? SplitNames(string s)
    {
        var arr = s.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return arr.Length == 0 ? null : arr;
    }
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
    private static string GetRaw(Dictionary<string, JsonElement>? p, string key)
        => p is null || !p.TryGetValue(key, out var el) ? ""
           : el.ValueKind == JsonValueKind.String ? (el.GetString() ?? "") : el.GetRawText();
}
