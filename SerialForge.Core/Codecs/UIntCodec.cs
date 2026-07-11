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
        // Tolerant parse: JSON fix values arrive as hex strings ("0x01") which
        // Convert.ToUInt64(object) rejects with FormatException. Accept numeric
        // primitives directly and parse strings as 0x-prefixed hex or decimal.
        ulong v = value switch
        {
            ulong u => u,
            long l => (ulong)l,
            int i => (ulong)i,
            string s when s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) => Convert.ToUInt64(s[2..], 16),
            string s => Convert.ToUInt64(s, 10),
            _ => Convert.ToUInt64(value)
        };
        int size = FixedSize!.Value;
        // Reject values that exceed the field width instead of silently
        // truncating the high bytes — spec §8 wants encode-time overflow surfaced.
        if (v >> (8 * size) != 0)
            throw new OverflowException($"value 0x{v:X} overflows {8 * size}-bit field");
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
        return (v, size);
    }
}
