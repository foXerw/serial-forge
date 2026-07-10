namespace SerialForge.Core.Codecs;

public sealed class EnumCodec : ICodec
{
    private readonly UIntCodec _inner;
    private readonly Dictionary<string, string> _nameToValue;

    public EnumCodec(CodecType underlying, Dictionary<string, string>? enumMap)
    {
        _inner = new UIntCodec(underlying);
        _nameToValue = new();
        if (enumMap != null)
        {
            foreach (var kv in enumMap)
                _nameToValue[kv.Value] = kv.Key;
        }
    }
    public int? FixedSize => _inner.FixedSize;

    public byte[] Encode(object value, int length, ByteOrder order)
    {
        // Accept either a name or a numeric/hex.
        if (value is string s && _nameToValue.TryGetValue(s, out var hex))
            return _inner.Encode(Convert.ToUInt64(hex.Replace("0x", ""), 16), length, order);
        return _inner.Encode(value, length, order);
    }

    public (object? Value, int Consumed) Decode(byte[] data, int offset, int length, ByteOrder order)
    {
        var (v, consumed) = _inner.Decode(data, offset, length, order);
        return (v, consumed); // engine maps to name; unknown stays numeric
    }
}
