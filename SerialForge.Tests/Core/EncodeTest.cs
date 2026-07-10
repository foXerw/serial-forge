using SerialForge.Core.Engine;
using SerialForge.Core.Models;

namespace SerialForge.Tests.Core;

public class EncodeTest
{
    private static ProtocolDefinition Def() =>
        ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-mcu.json"));

    [Fact]
    public void Encode_readVersion_frame()
    {
        var engine = new ProtocolEngine(Def());
        var inst = new CommandInstance { Command = Def().Commands[0] };
        // readVersion: cmd=0x01 (fixed), empty payload.
        var frame = engine.Encode(inst);
        // AA 55 | len(=1, just cmd counted? no: counts=payload(0)) -> len=0 | 01 | crc16
        // Recompute expected: len=0 => 00 00; payload empty; crc16 over AA 55 00 00 01
        Assert.Equal(new byte[] { 0xAA, 0x55 }, frame[..2]);
        Assert.Equal(0x00, frame[2]); // len lo
        Assert.Equal(0x00, frame[3]); // len hi
        Assert.Equal(0x01, frame[4]); // cmd
        Assert.Equal(7, frame.Length);
    }

    [Fact]
    public void Encode_writeConfig_packs_payload_subfields()
    {
        var engine = new ProtocolEngine(Def());
        var def = Def();
        var inst = new CommandInstance { Command = def.Commands[1] };
        inst.PayloadValues["id"] = 0x10;
        inst.PayloadValues["value"] = 0x1234;

        var frame = engine.Encode(inst);
        // payload = id(0x10) + value(0x1234 little -> 34 12) => 10 34 12
        Assert.Equal(new byte[] { 0x10, 0x34, 0x12 }, frame[5..8]);
        Assert.Equal(0x03, frame[2]); // len lo = 3
        Assert.Equal(0x00, frame[3]); // len hi
    }
}
