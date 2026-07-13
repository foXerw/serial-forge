using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SerialForge.Core;
using SerialForge.Core.Models;

namespace SerialForge.App.ViewModels;

public sealed partial class PayloadFieldViewModel : ViewModelBase
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private CodecType _codec = CodecType.U8;
    [ObservableProperty] private ByteOrder? _byteOrder;
    [ObservableProperty] private int? _size;
    [ObservableProperty] private string _default = "";

    public ObservableCollection<BitFieldEditorViewModel> Bits { get; } = new();
    [ObservableProperty] private bool _isBitField;

    public PayloadFieldViewModel() { }
    public PayloadFieldViewModel(PayloadFieldDef d) : this(d.Name, d.Codec, d.ByteOrder, d.Size, d.Default)
    {
        if (d.Bits is { } bs) { _isBitField = true; foreach (var b in bs) Bits.Add(new BitFieldEditorViewModel(b)); }
    }
    public PayloadFieldViewModel(string name, CodecType codec, ByteOrder? byteOrder, int? size, string? defaultValue)
    { _name = name; _codec = codec; _byteOrder = byteOrder; _size = size; _default = defaultValue ?? ""; }

    public PayloadFieldDef ToDef()
    {
        BitFieldDef[]? bits = IsBitField ? Bits.Select(b => b.ToDef()).ToArray() : null;
        var codec = bits is null ? Codec : CodecType.U8;
        return new PayloadFieldDef(Name, codec, ByteOrder, Size, Default == "" ? null : Default, bits);
    }
}
