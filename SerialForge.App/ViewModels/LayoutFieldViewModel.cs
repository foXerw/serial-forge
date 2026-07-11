using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using SerialForge.Core;
using SerialForge.Core.Models;

namespace SerialForge.App.ViewModels;

public sealed partial class LayoutFieldViewModel : ViewModelBase
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private FieldKind _kind = FieldKind.Value;
    [ObservableProperty] private CodecType _codec = CodecType.U8;
    [ObservableProperty] private ByteOrder? _byteOrder;     // null = use protocol default
    [ObservableProperty] private int? _size;
    [ObservableProperty] private string _literalValue = ""; // "AA 55"
    [ObservableProperty] private string _default = "";

    public ComputeEditorViewModel Compute { get; } = new();

    public LayoutFieldViewModel() { }
    public LayoutFieldViewModel(FieldDef f)
    {
        _name = f.Name; _kind = f.Kind; _codec = f.Codec; _byteOrder = f.ByteOrder; _size = f.Size;
        _literalValue = f.LiteralValue is null ? "" : string.Join(" ", f.LiteralValue);
        _default = f.Default ?? "";
        if (f.Compute is { } c) Compute = new ComputeEditorViewModel(c);
    }

    public FieldDef ToFieldDef(ByteOrder defaultOrder)
    {
        ComputeSpec? compute = Kind == FieldKind.Computed ? Compute.ToComputeSpec() : null;
        if (compute is { Algo: "length" })
        {
            var p = compute.Params ?? new Dictionary<string, JsonElement>();
            p["width"] = JsonSerializer.SerializeToElement(CodecWidth(Codec));
            compute = compute with { Params = p };
        }
        return new FieldDef(
            Name, Kind, Codec, ByteOrder, Size,
            Kind == FieldKind.Literal ? ParseLiteral(LiteralValue) : null,
            Kind == FieldKind.Value && Default != "" ? Default : null,
            null, compute);
    }

    private static string[]? ParseLiteral(string s)
    {
        var arr = s.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Select(NormalizeHex).ToArray();
        return arr.Length == 0 ? null : arr;
    }
    private static string NormalizeHex(string tok)
    {
        string digits = tok.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? tok[2..] : tok;
        return "0x" + digits.ToLowerInvariant();
    }
    private static int CodecWidth(CodecType c) => c switch
    {
        CodecType.U8 => 1, CodecType.U16 => 2, CodecType.U32 => 4, _ => 1
    };
}
