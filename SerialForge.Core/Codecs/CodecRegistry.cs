namespace SerialForge.Core.Codecs;

public sealed class CodecRegistry
{
    public ICodec Get(CodecType type) => type switch
    {
        CodecType.U8 or CodecType.U16 or CodecType.U32 => new UIntCodec(type),
        CodecType.Bytes => new BytesCodec(),
        CodecType.String => new StringCodec(),
        CodecType.Enum => new EnumCodec(type, null), // enum needs map; engine constructs with map
        CodecType.Raw => new RawCodec(),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
