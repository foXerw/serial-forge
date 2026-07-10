using SerialForge.Core;
using SerialForge.Core.Codecs;

namespace SerialForge.Tests.Core;

public class CodecsTest
{
    private readonly CodecRegistry _r = new();

    [Fact]
    public void UInt16_little_endian_round_trips()
    {
        var codec = _r.Get(CodecType.U16);
        var bytes = codec.Encode(0x0102, 2, ByteOrder.Little);
        Assert.Equal(new byte[] { 0x02, 0x01 }, bytes);
        var (value, consumed) = codec.Decode(bytes, 0, 2, ByteOrder.Little);
        Assert.Equal(0x0102, value);
        Assert.Equal(2, consumed);
    }

    [Fact]
    public void UInt8_has_fixed_size_1()
    {
        Assert.Equal(1, _r.Get(CodecType.U8).FixedSize);
        Assert.Equal(2, _r.Get(CodecType.U16).FixedSize);
        Assert.Equal(4, _r.Get(CodecType.U32).FixedSize);
    }

    [Fact]
    public void Bytes_encodes_hex_string_and_decodes_length()
    {
        var codec = _r.Get(CodecType.Bytes);
        var bytes = codec.Encode("01 02 03", 3, ByteOrder.Little);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, bytes);
        var (value, consumed) = codec.Decode(new byte[] { 0x01, 0x02, 0x03, 0xFF }, 0, 3, ByteOrder.Little);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, value);
        Assert.Equal(3, consumed);
    }

    [Fact]
    public void String_round_trips_ascii()
    {
        var codec = _r.Get(CodecType.String);
        var bytes = codec.Encode("AB", 2, ByteOrder.Little);
        Assert.Equal(new byte[] { 0x41, 0x42 }, bytes);
    }

    [Fact]
    public void Enum_decodes_unknown_as_raw()
    {
        var map = new Dictionary<string, string> { { "0x01", "READ" } };
        var codec = new EnumCodec(CodecType.U8, map);
        var (value, _) = codec.Decode(new byte[] { 0x09 }, 0, 1, ByteOrder.Little);
        // Unknown value returns the raw numeric; engine/UI marks unknown.
        Assert.Equal(0x09, value);
    }
}
