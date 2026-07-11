using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using SerialForge.Core.Algorithms;
using SerialForge.Core.Models;

namespace SerialForge.App.ViewModels;

// Edits one ComputeSpec. CRC params (poly/init/xorOut as hex strings, refIn/refOut
// as bools) are typed here and converted to/from the JsonElement Params dict.
// Blank hex fields are omitted so the algorithm falls back to its built-in default.
public sealed partial class ComputeEditorViewModel : ViewModelBase
{
    public static readonly string[] Algos = { "length", "crc16", "crc32", "sum8", "xor8" };

    [ObservableProperty] private string _algo = "length";
    [ObservableProperty] private string _counts = "";   // comma/space separated field names
    [ObservableProperty] private int _offset;
    [ObservableProperty] private string _from = "";
    [ObservableProperty] private string _to = "";
    [ObservableProperty] private string _poly = "";
    [ObservableProperty] private string _init = "";
    [ObservableProperty] private string _xorOut = "";
    [ObservableProperty] private bool _refIn;
    [ObservableProperty] private bool _refOut;

    public bool IsLength => Algo == "length";
    public bool IsCrc => Algo == "crc16" || Algo == "crc32";
    public bool IsSum => Algo == "sum8" || Algo == "xor8";

    partial void OnAlgoChanged(string value)
    {
        OnPropertyChanged(nameof(IsLength));
        OnPropertyChanged(nameof(IsCrc));
        OnPropertyChanged(nameof(IsSum));
    }

    public ComputeEditorViewModel() { }

    public ComputeEditorViewModel(ComputeSpec c)
    {
        _algo = c.Algo;
        _offset = c.Offset;
        _counts = c.Counts is null ? "" : string.Join(", ", c.Counts);
        _from = c.From ?? "";
        _to = c.To ?? "";
        _poly = GetRaw(c.Params, "poly");
        _init = GetRaw(c.Params, "init");
        _xorOut = GetRaw(c.Params, "xorOut");
        _refIn = c.Params.GetBool("refIn");
        _refOut = c.Params.GetBool("refOut");
    }

    public ComputeSpec ToComputeSpec()
    {
        var p = new Dictionary<string, JsonElement>();
        if (IsCrc)
        {
            AddHex(p, "poly", Poly);
            AddHex(p, "init", Init);
            AddHex(p, "xorOut", XorOut);
            p["refIn"] = JsonSerializer.SerializeToElement(RefIn);
            p["refOut"] = JsonSerializer.SerializeToElement(RefOut);
        }
        string[]? counts = IsLength ? SplitNames(Counts) : null;
        string? from = (IsCrc || IsSum) && !string.IsNullOrWhiteSpace(From) ? From.Trim() : null;
        string? to = (IsCrc || IsSum) && !string.IsNullOrWhiteSpace(To) ? To.Trim() : null;
        return new ComputeSpec(Algo, counts, Offset, from, to, p.Count == 0 ? null : p);
    }

    private static void AddHex(Dictionary<string, JsonElement> p, string key, string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return;   // omit -> algorithm default
        string digits = val.Trim().Replace("0x", "").Replace("0X", "");
        p[key] = JsonSerializer.SerializeToElement("0x" + digits.ToLowerInvariant());
    }

    private static string[]? SplitNames(string s)
    {
        var arr = s.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return arr.Length == 0 ? null : arr;
    }

    private static string GetRaw(Dictionary<string, JsonElement>? p, string key)
    {
        if (p is null || !p.TryGetValue(key, out var el)) return "";
        return el.ValueKind == JsonValueKind.String ? (el.GetString() ?? "") : el.GetRawText();
    }
}
