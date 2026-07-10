namespace SerialForge.Core.Codecs;

public sealed class EnumCodec : ICodec
{
    private readonly UIntCodec _inner;
    private readonly Dictionary<string, string> _nameToValue;
    private readonly Dictionary<ulong, string> _valueToName;

    public EnumCodec(CodecType underlying, Dictionary<string, string>? enumMap)
    {
        _inner = new UIntCodec(underlying);
        _nameToValue = enumMap ?? new();
        _valueToName = new();
        foreach (var kv in _nameToValue)
        {
            ulong v = Convert.ToUInt64(kv.Key.Replace("0x", ""), 16);
            _valueToName[v] = kv.Value;
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
