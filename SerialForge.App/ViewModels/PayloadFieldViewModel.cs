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

    public PayloadFieldViewModel() { }
    public PayloadFieldViewModel(PayloadFieldDef d) : this(d.Name, d.Codec, d.ByteOrder, d.Size, d.Default) { }
    public PayloadFieldViewModel(string name, CodecType codec, ByteOrder? byteOrder, int? size, string? defaultValue)
    { _name = name; _codec = codec; _byteOrder = byteOrder; _size = size; _default = defaultValue ?? ""; }

    public PayloadFieldDef ToDef() => new(Name, Codec, ByteOrder, Size, Default == "" ? null : Default);
}
