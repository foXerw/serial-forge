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

    [Fact]
    public void Decode_emits_one_field_per_layout_bit_child()
    {
        var def = Def();
        var engine = new ProtocolEngine(def);
        var inst = new CommandInstance { Command = def.Commands[0] };
        inst.PayloadValues["flags.type"] = 0x5UL;
        inst.PayloadValues["flags.subtype"] = 0x3UL;
        inst.PayloadValues["seq"] = 0x7UL;
        var decoded = engine.Decode(engine.Encode(inst));
        Assert.Null(decoded.Error);
        Assert.Equal(1UL, decoded.Fields.First(f => f.Name == "status.type").Value);
        Assert.Equal(0UL, decoded.Fields.First(f => f.Name == "status.subtype").Value);
    }

    [Fact]
    public void Decode_layout_bitfield_child_renders_enum_name()
    {
        var json = """
        {
          "name": "enum-bits", "version": "1.0", "defaultByteOrder": "little",
          "framing": { "mode": "length-prefix", "preamble": ["0xAA"], "lengthField": "len", "frameTimeoutMs": 50 },
          "layout": [
            { "name": "preamble", "kind": "literal", "codec": "bytes", "value": ["0xAA"] },
            { "name": "len", "kind": "computed", "codec": "u8", "compute": { "algo": "length", "counts": ["payload"], "params": { "width": 1 } } },
            { "name": "status", "kind": "value", "codec": "u8",
              "bits": [ { "name": "type", "offset": 0, "width": 4, "default": "0x1", "enum": { "1": "run" } } ] },
            { "name": "payload", "kind": "value", "codec": "bytes" }
          ],
          "commands": [ { "name": "ping", "title": "Ping", "fix": {}, "payloadFields": [] } ]
        }
        """;
        var def = ProtocolLoader.Load(json);
        var engine = new ProtocolEngine(def);
        var decoded = engine.Decode(engine.Encode(new CommandInstance { Command = def.Commands[0] }));
        Assert.Null(decoded.Error);
        Assert.Equal("run", decoded.Fields.First(f => f.Name == "status.type").Value);
    }

    // len byte is shared: high nibble = version (default 1), low nibble = the
    // auto-computed payload length. Exercises the computed-length bitfield path.
    private static ProtocolDefinition LengthBitFieldDef()
    {
        var json = """
        {
          "name": "len-bits", "version": "1.0", "defaultByteOrder": "little",
          "framing": { "mode": "length-prefix", "preamble": ["0xAA"], "lengthField": "len", "frameTimeoutMs": 50 },
          "layout": [
            { "name": "preamble", "kind": "literal", "codec": "bytes", "value": ["0xAA"] },
            { "name": "len", "kind": "computed", "codec": "u8", "compute": { "algo": "length", "counts": ["payload"] },
              "bits": [
                { "name": "ver", "offset": 0, "width": 4, "default": "0x1" },
                { "name": "size", "offset": 4, "width": 4, "isLength": true }
              ] },
            { "name": "payload", "kind": "value", "codec": "bytes" }
          ],
          "commands": [ { "name": "ping", "title": "Ping", "fix": {}, "payloadFields": [] } ]
        }
        """;
        return ProtocolLoader.Load(json);
    }

    [Fact]
    public void Encode_packs_length_into_bitfield_alongside_other_bits()
    {
        var engine = new ProtocolEngine(LengthBitFieldDef());
        var inst = new CommandInstance { Command = engine.Definition.Commands[0] };
        inst.FieldValues["payload"] = "0x01 0x02 0x03";   // 3 payload bytes
        var frame = engine.Encode(inst);
        // AA | len(ver=1<<4 | size=3) | 01 02 03
        Assert.Equal(0xAA, frame[0]);
        Assert.Equal(0x13, frame[1]);
        Assert.Equal(new byte[] { 0xAA, 0x13, 0x01, 0x02, 0x03 }, frame);
    }

    [Fact]
    public void Encode_length_overflowing_bit_width_throws()
    {
        var engine = new ProtocolEngine(LengthBitFieldDef());
        var inst = new CommandInstance { Command = engine.Definition.Commands[0] };
        inst.FieldValues["payload"] = string.Join(" ", Enumerable.Range(0, 16).Select(i => "0x01"));  // 16 bytes > 4-bit max
        Assert.Throws<ProtocolException>(() => engine.Encode(inst));
    }

    [Fact]
    public void Decode_expands_length_bitfield_children()
    {
        var engine = new ProtocolEngine(LengthBitFieldDef());
        var inst = new CommandInstance { Command = engine.Definition.Commands[0] };
        inst.FieldValues["payload"] = "0x01 0x02 0x03";
        var decoded = engine.Decode(engine.Encode(inst));
        Assert.Null(decoded.Error);
        Assert.Equal(1UL, decoded.Fields.First(f => f.Name == "len.ver").Value);
        Assert.Equal(3UL, decoded.Fields.First(f => f.Name == "len.size").Value);
    }

    [Fact]
    public void Decode_tampered_length_bits_report_mismatch()
    {
        // len counts the fixed preamble (not the variable payload), so the length
        // check is non-tautological and a tampered size nibble is caught.
        var json = """
        {
          "name": "len-bits-fix", "version": "1.0", "defaultByteOrder": "little",
          "framing": { "mode": "length-prefix", "preamble": ["0xAA"], "lengthField": "len", "frameTimeoutMs": 50 },
          "layout": [
            { "name": "preamble", "kind": "literal", "codec": "bytes", "value": ["0xAA"] },
            { "name": "len", "kind": "computed", "codec": "u8", "compute": { "algo": "length", "counts": ["preamble"] },
              "bits": [
                { "name": "ver", "offset": 0, "width": 4, "default": "0x1" },
                { "name": "size", "offset": 4, "width": 4, "isLength": true }
              ] },
            { "name": "payload", "kind": "value", "codec": "bytes" }
          ],
          "commands": [ { "name": "ping", "title": "Ping", "fix": {}, "payloadFields": [] } ]
        }
        """;
        var engine = new ProtocolEngine(ProtocolLoader.Load(json));
        var frame = engine.Encode(new CommandInstance { Command = engine.Definition.Commands[0] });
        Assert.Equal(0x11, frame[1]);   // ver=1<<4 | size(preamble=1)
        frame[1] = 0x12;                // tamper size nibble 1 -> 2
        var decoded = engine.Decode(frame);
        Assert.NotNull(decoded.Error);
        Assert.Contains("length", decoded.Error, StringComparison.OrdinalIgnoreCase);
    }
}
