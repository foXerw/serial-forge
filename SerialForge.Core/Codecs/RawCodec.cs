namespace SerialForge.Core.Codecs;

/// <summary>Pass-through sink: consumes whatever bytes the engine assigns.</summary>
public sealed class RawCodec : ICodec
{
    public int? FixedSize => null;
    public byte[] Encode(object value, int length, ByteOrder order) =>
        value is byte[] b ? b : BytesCodec.ParseHex(value.ToString()!);
    public (object? Value, int Consumed) Decode(byte[] data, int offset, int length, ByteOrder order) =>
        (new ArraySegment<byte>(data, offset, length).ToArray(), length);
}
