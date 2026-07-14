using SerialForge.Core.Engine;
using SerialForge.Core.Exceptions;
using SerialForge.Core.Models;
using SerialForge.Core;

namespace SerialForge.Tests.Core;

public class ValidateTest
{
    private static ProtocolDefinition Def() =>
        ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-mcu.json"));

    [Fact]
    public void Valid_definition_passes()
    {
        var ex = Record.Exception(() => ProtocolLoader.Validate(Def()));
        Assert.Null(ex);
    }

    [Fact]
    public void Missing_name_throws()
    {
        var d = Def() with { Name = "" };
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(d));
    }

    [Fact]
    public void Missing_layout_throws()
    {
        var d = Def() with { Layout = Array.Empty<FieldDef>() };
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(d));
    }

    [Fact]
    public void Compute_over_unknown_field_throws()
    {
        var d = Def();
        var computed = Array.Find(d.Layout, f => f.Compute is not null);
        Assert.NotNull(computed);
        var bad = computed! with { Compute = computed!.Compute! with { To = "no_such_field" } };
        var layout = d.Layout.Select(f => f == computed ? bad : f).ToArray();
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(d with { Layout = layout }));
    }

    [Fact]
    public void Bitfield_on_non_u8_codec_rejected()
    {
        var def = ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-bits.json"));
        var status = def.Layout.First(f => f.Name == "status");
        var bad = status with { Codec = CodecType.U16 };
        var layout = def.Layout.Select(f => f == status ? bad : f).ToArray();
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(def with { Layout = layout }));
    }

    [Fact]
    public void Bitfield_overlapping_ranges_rejected()
    {
        var def = ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-bits.json"));
        var status = def.Layout.First(f => f.Name == "status");
        var overlap = status with
        {
            Bits = new[] { new BitFieldDef("a", 0, 5, null, null), new BitFieldDef("b", 3, 4, null, null) }
        };
        var layout = def.Layout.Select(f => f == status ? overlap : f).ToArray();
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(def with { Layout = layout }));
    }

    [Fact]
    public void Bitfield_out_of_range_offset_rejected()
    {
        var def = ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-bits.json"));
        var status = def.Layout.First(f => f.Name == "status");
        var bad = status with { Bits = new[] { new BitFieldDef("a", 4, 5, null, null) } }; // 4+5>8
        var layout = def.Layout.Select(f => f == status ? bad : f).ToArray();
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(def with { Layout = layout }));
    }

    [Fact]
    public void Bitfield_on_literal_field_rejected()
    {
        var def = ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-bits.json"));
        var status = def.Layout.First(f => f.Name == "status");
        var bad = status with { Kind = FieldKind.Literal, LiteralValue = new[] { "0x00" }, Bits = status.Bits };
        var layout = def.Layout.Select(f => f == status ? bad : f).ToArray();
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(def with { Layout = layout }));
    }

    // A length-prefix byte shared with other attributes: high nibble = version,
    // low nibble = auto-computed payload length. `isLengthCount` flags that many
    // children as the length carrier (1 = valid; 0 or 2 = invalid).
    private static ProtocolDefinition LengthBitFieldDef(int isLengthCount)
    {
        var bits = new[]
        {
            new BitFieldDef("ver", 0, 4, null, "0x1", isLengthCount >= 1),
            new BitFieldDef("size", 4, 4, null, null, isLengthCount >= 2)
        };
        var json = """
        {
          "name": "len-bits", "version": "1.0", "defaultByteOrder": "little",
          "framing": { "mode": "length-prefix", "preamble": ["0xAA"], "lengthField": "len", "frameTimeoutMs": 50 },
          "layout": [
            { "name": "preamble", "kind": "literal", "codec": "bytes", "value": ["0xAA"] },
            { "name": "len", "kind": "computed", "codec": "u8", "compute": { "algo": "length", "counts": ["payload"] } },
            { "name": "payload", "kind": "value", "codec": "bytes" }
          ],
          "commands": [ { "name": "ping", "title": "Ping", "fix": {}, "payloadFields": [] } ]
        }
        """;
        var def = ProtocolLoader.Load(json);
        var len = def.Layout.First(f => f.Name == "len");
        var patched = len with { Bits = bits };
        return def with { Layout = def.Layout.Select(f => f == len ? patched : f).ToArray() };
    }

    [Fact]
    public void Length_bitfield_with_one_IsLength_child_passes()
    {
        var ex = Record.Exception(() => ProtocolLoader.Validate(LengthBitFieldDef(1)));
        Assert.Null(ex);
    }

    [Fact]
    public void Length_bitfield_with_no_IsLength_child_rejected()
    {
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(LengthBitFieldDef(0)));
    }

    [Fact]
    public void Length_bitfield_with_two_IsLength_children_rejected()
    {
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(LengthBitFieldDef(2)));
    }

    [Fact]
    public void Value_bitfield_with_IsLength_child_rejected()
    {
        var def = ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-bits.json"));
        var status = def.Layout.First(f => f.Name == "status") with
        {
            Bits = new[] { new BitFieldDef("type", 0, 4, null, null, true) }   // IsLength on a value bit
        };
        var layout = def.Layout.Select(f => f.Name == "status" ? status : f).ToArray();
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(def with { Layout = layout }));
    }

    [Fact]
    public void Crc_computed_bitfield_rejected()
    {
        var def = ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-bits.json"));
        var crc = def.Layout.First(f => f.Name == "crc16") with
        {
            Codec = CodecType.U8,
            Bits = new[] { new BitFieldDef("lo", 0, 4, null, null, true) }
        };
        var layout = def.Layout.Select(f => f.Name == "crc16" ? crc : f).ToArray();
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(def with { Layout = layout }));
    }
}
