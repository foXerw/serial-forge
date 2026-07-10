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
}
