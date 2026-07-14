using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    public ObservableCollection<BitFieldEditorViewModel> Bits { get; } = new();
    [ObservableProperty] private bool _isBitField;

    // True when this row is a length-prefix field that may share its byte with
    // other attributes — its bit children then expose the IsLength selector.
    public bool IsLengthField => Kind == FieldKind.Computed && Compute.IsLength;

    public LayoutFieldViewModel() { Compute.PropertyChanged += OnComputeChanged; }
    public LayoutFieldViewModel(FieldDef f)
    {
        _name = f.Name; _kind = f.Kind; _codec = f.Codec; _byteOrder = f.ByteOrder; _size = f.Size;
        _literalValue = f.LiteralValue is null ? "" : string.Join(" ", f.LiteralValue);
        _default = f.Default ?? "";
        if (f.Compute is { } c) Compute = new ComputeEditorViewModel(c);
        Compute.PropertyChanged += OnComputeChanged;
        if (f.Bits is { } bs) { _isBitField = true; foreach (var b in bs) Bits.Add(MakeBit(b)); }
        RefreshBitFlags();
    }

    partial void OnKindChanged(FieldKind value)
    {
        OnPropertyChanged(nameof(IsLengthField));
        RefreshBitFlags();
    }

    // React to algo switches (length <-> crc/sum) in the compute editor.
    private void OnComputeChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ComputeEditorViewModel.Algo) or nameof(ComputeEditorViewModel.IsLength))
        {
            OnPropertyChanged(nameof(IsLengthField));
            RefreshBitFlags();
        }
    }

    private void RefreshBitFlags()
    {
        foreach (var b in Bits) b.CanSetIsLength = IsLengthField;
    }

    // Radio behavior: marking a child as the length carrier clears the others,
    // since a length byte has exactly one length child.
    private void OnBitChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BitFieldEditorViewModel.IsLength)) return;
        if (s is BitFieldEditorViewModel bit && bit.IsLength)
            foreach (var other in Bits)
                if (!ReferenceEquals(other, bit)) other.IsLength = false;
    }

    private BitFieldEditorViewModel MakeBit(BitFieldDef? d = null)
    {
        var b = d is null ? new BitFieldEditorViewModel() : new BitFieldEditorViewModel(d);
        b.CanSetIsLength = IsLengthField;
        b.PropertyChanged += OnBitChanged;
        return b;
    }

    public FieldDef ToFieldDef()
    {
        ComputeSpec? compute = Kind == FieldKind.Computed ? Compute.ToComputeSpec() : null;
        if (compute is { Algo: "length" })
        {
            var p = compute.Params ?? new Dictionary<string, JsonElement>();
            p["width"] = JsonSerializer.SerializeToElement(CodecWidth(Codec));
            compute = compute with { Params = p };
        }
        BitFieldDef[]? bits = IsBitField ? BuildBits() : null;
        var codec = bits is null ? Codec : CodecType.U8;
        return new FieldDef(
            Name, Kind, codec, ByteOrder, Size,
            Kind == FieldKind.Literal ? ParseLiteral(LiteralValue) : null,
            Kind == FieldKind.Value && Default != "" ? Default : null,
            null, compute, bits);
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

    // Build the bit defs, guaranteeing a length bitfield has exactly one IsLength
    // child (auto-mark the first if none, drop extras if many). The radio handler
    // usually keeps the UI in shape; this is the deterministic safety net.
    private BitFieldDef[] BuildBits()
    {
        var defs = Bits.Select(b => b.ToDef()).ToArray();
        if (IsLengthField && defs.Length > 0)
        {
            int first = Array.FindIndex(defs, d => d.IsLength);
            if (first < 0) defs[0] = defs[0] with { IsLength = true };
            else
                for (int i = 0; i < defs.Length; i++)
                    if (i != first && defs[i].IsLength) defs[i] = defs[i] with { IsLength = false };
        }
        return defs;
    }

    [RelayCommand]
    private void AddBit() => Bits.Add(MakeBit());

    [RelayCommand]
    private void RemoveBit(BitFieldEditorViewModel? b)
    {
        if (b is not null) { b.PropertyChanged -= OnBitChanged; Bits.Remove(b); }
    }
}
