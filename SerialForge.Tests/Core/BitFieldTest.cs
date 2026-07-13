using SerialForge.Core.Engine;
using SerialForge.Core.Exceptions;
using SerialForge.Core.Models;

namespace SerialForge.Tests.Core;

public class BitFieldTest
{
    private static ProtocolDefinition Def() =>
        ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-bits.json"));

    [Fact]
    public void Encode_packs_layout_and_payload_bitfields()
    {
        var def = Def();
        var engine = new ProtocolEngine(def);
        var inst = new CommandInstance { Command = def.Commands[0] };
        inst.PayloadValues["flags.type"] = 0x5UL;
        inst.PayloadValues["flags.subtype"] = 0x3UL;
        inst.PayloadValues["seq"] = 0x7UL;
        var frame = engine.Encode(inst);
        // AA 55 | len(02 00) | cmd(10) | status(10: type default 1<<4) | flags(53) seq(07) | crc16
        Assert.Equal(0x10, frame[4]);   // cmd fixed
        Assert.Equal(0x10, frame[5]);   // status: type=1(default) <<4 | subtype=0
        Assert.Equal(0x53, frame[6]);   // flags: type=5<<4 | subtype=3
        Assert.Equal(0x07, frame[7]);   // seq
        Assert.Equal(0x02, frame[2]);   // len lo = payload length 2
    }

    [Fact]
    public void Encode_overwide_bit_value_throws()
    {
        var def = Def();
        var engine = new ProtocolEngine(def);
        var inst = new CommandInstance { Command = def.Commands[0] };
        inst.PayloadValues["flags.type"] = 0x1FUL;   // 5 bits into 4-bit field
        Assert.Throws<ProtocolException>(() => engine.Encode(inst));
    }
}
