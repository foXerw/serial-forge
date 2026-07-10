namespace SerialForge.Core.Codecs;

public sealed class UIntCodec : ICodec
{
    private readonly CodecType _type;
    public UIntCodec(CodecType type) => _type = type;
    public int? FixedSize => _type switch
    {
        CodecType.U8 => 1,
        CodecType.U16 => 2,
        CodecType.U32 => 4,
        _ => null
    };

    public byte[] Encode(object value, int length, ByteOrder order)
    {
        ulong v = Convert.ToUInt64(value);
        int size = FixedSize!.Value;
        var bytes = new byte[size];
        for (int i = 0; i < size; i++)
            bytes[i] = (byte)(v >> (8 * i));
        return order == ByteOrder.Little ? bytes : bytes.Reverse().ToArray();
    }

    public (object? Value, int Consumed) Decode(byte[] data, int offset, int length, ByteOrder order)
    {
        int size = FixedSize!.Value;
        ulong v = 0;
        for (int i = 0; i < size; i++)
        {
            int bi = order == ByteOrder.Little ? offset + i : offset + size - 1 - i;
            v |= ((ulong)data[bi]) << (8 * i);
        }
        // Box as int where the value fits (matches callers' int literals and
        // all byte/ushort values); keep ulong for U32 values exceeding int.MaxValue.
        object boxed = v <= int.MaxValue ? (object)(int)v : v;
        return (boxed, size);
    }
}
