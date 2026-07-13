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
}
