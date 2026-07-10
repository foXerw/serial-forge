using SerialForge.Core.Engine;
using SerialForge.Core.Models;

namespace SerialForge.Tests.Core;

public class DecodeTest
{
    private static ProtocolDefinition Def() =>
        ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-mcu.json"));

    [Fact]
    public void Decode_round_trips_an_encoded_frame()
    {
        var def = Def();
        var engine = new ProtocolEngine(def);
        var inst = new CommandInstance { Command = def.Commands[1] };
        inst.PayloadValues["id"] = 0x10;
        inst.PayloadValues["value"] = 0x1234;
        var frame = engine.Encode(inst);

        var decoded = engine.Decode(frame);
        Assert.Null(decoded.Error);
        var cmd = decoded.Fields.First(f => f.Name == "cmd");
        Assert.Equal(0x05UL, cmd.Value);
    }

    [Fact]
    public void Decode_bad_crc_marks_error_without_throwing()
    {
        var def = Def();
        var engine = new ProtocolEngine(def);
        var inst = new CommandInstance { Command = def.Commands[0] };
        var frame = engine.Encode(inst);
        frame[^1] ^= 0xFF; // corrupt last CRC byte

        var decoded = engine.Decode(frame);
        Assert.NotNull(decoded.Error);
        Assert.Contains("crc", decoded.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decode_truncated_frame_returns_error()
    {
        var engine = new ProtocolEngine(Def());
        var decoded = engine.Decode(new byte[] { 0xAA, 0x55 });
        Assert.NotNull(decoded.Error);
    }
}
