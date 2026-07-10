namespace SerialForge.Core.Codecs;

public sealed class BytesCodec : ICodec
{
    public int? FixedSize => null;

    public byte[] Encode(object value, int length, ByteOrder order) => value switch
    {
        byte[] b => b,
        string s => ParseHex(s),
        _ => throw new ArgumentException("bytes value must be byte[] or hex string")
    };

    public (object? Value, int Consumed) Decode(byte[] data, int offset, int length, ByteOrder order)
    {
        var slice = new ArraySegment<byte>(data, offset, length).ToArray();
        return (slice, length);
    }

    public static byte[] ParseHex(string s)
    {
        var clean = s.Replace(" ", "").Replace("0x", "");
        if (clean.Length % 2 != 0) throw new ArgumentException("odd-length hex");
        var bytes = new byte[clean.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = byte.Parse(clean.AsSpan(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
        return bytes;
    }
}
