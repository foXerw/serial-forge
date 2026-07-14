using System.Collections.Generic;
using System.Text.Json;
using SerialForge.Core;
using SerialForge.Core.Engine;
using SerialForge.Core.Models;

namespace SerialForge.Tests.Core;

public class FrameEngineTest
{
    private static ByteOrder L => ByteOrder.Little;

    private static Segment[] DemoMcuFrame() => new[]
    {
        new Segment("preamble", SegmentRole.Fixed, 16, null, new[] { "0xAA", "0x55" }, null, null, null, 0, null, null, null, null),
        new Segment("len", SegmentRole.Length, 16, L, null, null, null, new[] { "payload" }, 0, null, null, null, null),
        new Segment("cmd", SegmentRole.Value, 8, null, null, null, null, null, 0, null, null, null, null),
        new Segment("payload", SegmentRole.Value, null, null, null, null, null, null, 0, null, null, null, null),
        new Segment("crc", SegmentRole.Checksum, 16, L, null, null, null, null, 0, "crc16", "preamble", "payload", new()
        {
            ["poly"] = JsonSerializer.SerializeToElement("0x1021"),
            ["init"] = JsonSerializer.SerializeToElement("0xFFFF"),
            ["refIn"] = JsonSerializer.SerializeToElement(false),
            ["refOut"] = JsonSerializer.SerializeToElement(false),
            ["xorOut"] = JsonSerializer.SerializeToElement("0x0000"),
        }),
    };

    [Fact]
    public void Pack_demo_mcu_readVersion_matches_golden_bytes()
    {
        var engine = new FrameEngine(DemoMcuFrame(), L);
        var values = new Dictionary<string, object> { ["cmd"] = 0x01 };
        var frame = engine.Pack(values);
        Assert.Equal(new byte[] { 0xAA, 0x55, 0x00, 0x00, 0x01, 0x99, 0xA4 }, frame);
    }

    [Fact]
    public void Parse_demo_mcu_readVersion_round_trips()
    {
        var engine = new FrameEngine(DemoMcuFrame(), L);
        var frame = engine.Pack(new Dictionary<string, object> { ["cmd"] = 0x01 });
        var decoded = engine.Parse(frame);
        Assert.Null(decoded.Error);
        Assert.Equal(0UL, decoded.Fields.First(f => f.Name == "len").Value);
        Assert.Equal(1UL, decoded.Fields.First(f => f.Name == "cmd").Value);
    }

    [Fact]
    public void Pack_and_parse_bitfield_shared_byte()
    {
        // ver (high nibble, default 1) + len (low nibble, length of payload) share one byte.
        var frame = new[]
        {
            new Segment("ver", SegmentRole.Value, 4, null, null, "0x1", null, null, 0, null, null, null, null),
            new Segment("len", SegmentRole.Length, 4, null, null, null, null, new[] { "payload" }, 0, null, null, null, null),
            new Segment("payload", SegmentRole.Value, null, null, null, null, null, null, 0, null, null, null, null),
        };
        var engine = new FrameEngine(frame, L);
        var packed = engine.Pack(new Dictionary<string, object> { ["payload"] = "0x01 0x02 0x03" });
        Assert.Equal(0x13, packed[0]);   // ver=1<<4 | len=3
        var decoded = engine.Parse(packed);
        Assert.Null(decoded.Error);
        Assert.Equal(1UL, decoded.Fields.First(f => f.Name == "ver").Value);
        Assert.Equal(3UL, decoded.Fields.First(f => f.Name == "len").Value);
    }

    // demo-bits reimagined: status and flags are split into nibble segments; the
    // length field counts the payload region (flags.type+flags.subtype+seq = 2 bytes).
    private static Segment[] DemoBitsFrame() => new[]
    {
        new Segment("preamble", SegmentRole.Fixed, 16, null, new[] { "0xAA", "0x55" }, null, null, null, 0, null, null, null, null),
        new Segment("len", SegmentRole.Length, 16, L, null, null, null, new[] { "flags.type", "flags.subtype", "seq" }, 0, null, null, null, null),
        new Segment("cmd", SegmentRole.Value, 8, null, null, null, null, null, 0, null, null, null, null),
        new Segment("status.type", SegmentRole.Value, 4, null, null, "0x1", null, null, 0, null, null, null, null),
        new Segment("status.subtype", SegmentRole.Value, 4, null, null, null, null, null, 0, null, null, null, null),
        new Segment("flags.type", SegmentRole.Value, 4, null, null, null, null, null, 0, null, null, null, null),
        new Segment("flags.subtype", SegmentRole.Value, 4, null, null, null, null, null, 0, null, null, null, null),
        new Segment("seq", SegmentRole.Value, 8, null, null, null, null, null, 0, null, null, null, null),
        new Segment("crc", SegmentRole.Checksum, 16, L, null, null, null, null, 0, "crc16", "preamble", "seq", new()
        {
            ["poly"] = JsonSerializer.SerializeToElement("0x1021"),
            ["init"] = JsonSerializer.SerializeToElement("0xFFFF"),
            ["refIn"] = JsonSerializer.SerializeToElement(false),
            ["refOut"] = JsonSerializer.SerializeToElement(false),
            ["xorOut"] = JsonSerializer.SerializeToElement("0x0000"),
        }),
    };

    [Fact]
    public void Pack_demo_bits_matches_golden_bytes()
    {
        var engine = new FrameEngine(DemoBitsFrame(), L);
        var values = new Dictionary<string, object>
        {
            ["cmd"] = 0x10,
            ["status.type"] = 0x1,     // default, restated for clarity
            ["flags.type"] = 0x5,
            ["flags.subtype"] = 0x3,
            ["seq"] = 0x7,
        };
        var frame = engine.Pack(values);
        Assert.Equal(0x02, frame[2]);   // len lo = payload 2 bytes
        Assert.Equal(0x10, frame[4]);   // cmd
        Assert.Equal(0x10, frame[5]);   // status: type=1<<4 | subtype=0
        Assert.Equal(0x53, frame[6]);   // flags: type=5<<4 | subtype=3
        Assert.Equal(0x07, frame[7]);   // seq
    }

    [Fact]
    public void Parse_demo_bits_round_trips()
    {
        var engine = new FrameEngine(DemoBitsFrame(), L);
        var values = new Dictionary<string, object>
        {
            ["cmd"] = 0x10, ["flags.type"] = 0x5, ["flags.subtype"] = 0x3, ["seq"] = 0x7,
        };
        var decoded = engine.Parse(engine.Pack(values));
        Assert.Null(decoded.Error);
        Assert.Equal(2UL, decoded.Fields.First(f => f.Name == "len").Value);
        Assert.Equal(5UL, decoded.Fields.First(f => f.Name == "flags.type").Value);
        Assert.Equal(7UL, decoded.Fields.First(f => f.Name == "seq").Value);
    }
}
