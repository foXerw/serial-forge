using SerialForge.Core;
using SerialForge.Core.Engine;
using SerialForge.Core.Exceptions;

namespace SerialForge.Tests.Core;

public class ProtocolLoaderTest
{
    private string Fixture => File.ReadAllText("Fixtures/demo-mcu.json");

    [Fact]
    public void Loads_demo_protocol()
    {
        var def = ProtocolLoader.Load(Fixture);
        Assert.Equal("demo-mcu", def.Name);
        Assert.Equal(ByteOrder.Little, def.DefaultByteOrder);
        Assert.Equal(5, def.Layout.Length);
        Assert.Equal("len", def.Framing.LengthField);
        Assert.Equal(2, def.Commands.Length);
    }

    [Fact]
    public void Rejects_dangling_length_reference()
    {
        var bad = Fixture.Replace("\"lengthField\": \"len\"", "\"lengthField\": \"nope\"");
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Load(bad));
    }

    [Fact]
    public void Rejects_unknown_field_in_compute_over()
    {
        var bad = Fixture.Replace("\"to\": \"payload\"", "\"to\": \"ghost\"");
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Load(bad));
    }

    [Fact]
    public void Rejects_missing_layout_with_protocol_exception()
    {
        // Valid protocol JSON but with the "layout" section omitted entirely:
        // must surface a clear ProtocolException (not a raw NullReferenceException).
        var json = """
        {
            "name": "demo-mcu",
            "version": "1.0.0",
            "defaultByteOrder": "little",
            "framing": { "mode": "length-prefix", "preamble": ["AA 55"], "lengthField": "len" },
            "commands": []
        }
        """;
        var ex = Assert.Throws<ProtocolException>(() => ProtocolLoader.Load(json));
        Assert.Contains("layout", ex.Message);
    }

    [Fact]
    public void Loads_bitfield_layout_and_payload()
    {
        var def = ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-bits.json"));
        var status = def.Layout.First(f => f.Name == "status");
        Assert.NotNull(status.Bits);
        Assert.Equal(2, status.Bits!.Length);
        Assert.Equal(4, status.Bits[0].Width);
        Assert.Equal("0x1", status.Bits[0].Default);
        var flags = def.Commands[0].PayloadFields.First(p => p.Name == "flags");
        Assert.NotNull(flags.Bits);
        Assert.Equal(2, flags.Bits!.Length);
    }
}
