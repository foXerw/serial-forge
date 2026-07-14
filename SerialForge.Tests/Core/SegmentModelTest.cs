using System.Text.Json;
using SerialForge.Core;
using SerialForge.Core.Engine;
using SerialForge.Core.Exceptions;
using SerialForge.Core.Models;
using SerialForge.Core.SegmentModel;
using SegLoader = SerialForge.Core.SegmentModel.ProtocolLoader;
using SegSaver = SerialForge.Core.SegmentModel.ProtocolSaver;

namespace SerialForge.Tests.Core;

public class SegmentModelTest
{
    private static readonly string DemoMcuJson = """
    {
      "name": "demo-mcu", "version": "2.0", "defaultByteOrder": "little",
      "frame": [
        { "name": "preamble", "role": "fixed", "width": 16, "value": ["0xAA","0x55"] },
        { "name": "len", "role": "length", "width": 16, "byteOrder": "little", "counts": ["payload"] },
        { "name": "cmd", "role": "value", "width": 8 },
        { "name": "payload", "role": "value" },
        { "name": "crc", "role": "checksum", "width": 16, "byteOrder": "little", "algo": "crc16",
          "over": { "from": "preamble", "to": "payload" },
          "params": { "poly":"0x1021","init":"0xFFFF","refIn":false,"refOut":false,"xorOut":"0x0000" } }
      ],
      "commands": [ { "name": "readVersion", "title": "Read Version", "values": { "cmd": "0x01" } } ]
    }
    """;

    [Fact]
    public void Load_then_pack_matches_golden_bytes()
    {
        var def = SegLoader.Load(DemoMcuJson);
        var engine = new FrameEngine(def.Frame, def.DefaultByteOrder);
        var frame = engine.Pack(new Dictionary<string, object> { ["cmd"] = 0x01 });
        Assert.Equal(new byte[] { 0xAA, 0x55, 0x00, 0x00, 0x01, 0x99, 0xA4 }, frame);
    }

    [Fact]
    public void Load_reads_command_values()
    {
        var def = SegLoader.Load(DemoMcuJson);
        Assert.Equal("demo-mcu", def.Name);
        Assert.Single(def.Commands);
        Assert.Equal("0x01", def.Commands[0].Values["cmd"]);
    }

    [Fact]
    public void Saver_round_trips_key_fields()
    {
        var def = SegLoader.Load(DemoMcuJson);
        var again = SegLoader.Load(SegSaver.ToJson(def));
        Assert.Equal(def.Name, again.Name);
        Assert.Equal(def.Frame.Length, again.Frame.Length);
        Assert.Equal("payload", again.Frame.First(f => f.Role == SegmentRole.Length)!.Counts![0]);
    }

    [Fact]
    public void Validate_rejects_duplicate_segment_names()
    {
        var def = SegLoader.Load(DemoMcuJson);
        var dup = def with { Frame = def.Frame.Select(s => s.Name == "cmd" ? s with { Name = "preamble" } : s).ToArray() };
        Assert.Throws<ProtocolException>(() => SegLoader.Validate(dup));
    }

    [Fact]
    public void Validate_rejects_two_variable_segments()
    {
        var def = SegLoader.Load(DemoMcuJson);
        var bad = def with { Frame = def.Frame.Select(s => s.Name == "cmd" ? s with { Width = null } : s).ToArray() };
        Assert.Throws<ProtocolException>(() => SegLoader.Validate(bad));
    }

    [Fact]
    public void Validate_rejects_length_not_preceding_payload()
    {
        // put the variable payload BEFORE the length field
        var def = SegLoader.Load(DemoMcuJson);
        var reordered = new[] { def.Frame[0], def.Frame[3], def.Frame[1], def.Frame[2], def.Frame[4] };
        var bad = def with { Frame = reordered };
        Assert.Throws<ProtocolException>(() => SegLoader.Validate(bad));
    }

    [Fact]
    public void Validate_rejects_checksum_over_self()
    {
        var def = SegLoader.Load(DemoMcuJson);
        var crc = def.Frame.First(f => f.Name == "crc");
        var bad = def with { Frame = def.Frame.Select(s => s.Name == "crc" ? s with { OverTo = "crc" } : s).ToArray() };
        Assert.Throws<ProtocolException>(() => SegLoader.Validate(bad));
    }

    [Fact]
    public void Validate_rejects_fixed_width_not_matching_value_bytes()
    {
        var def = SegLoader.Load(DemoMcuJson);
        var bad = def with { Frame = def.Frame.Select(s => s.Name == "preamble" ? s with { Width = 8 } : s).ToArray() };
        Assert.Throws<ProtocolException>(() => SegLoader.Validate(bad));
    }

    private static readonly string DemoBitsJson = """
    {
      "name": "demo-bits", "version": "2.0", "defaultByteOrder": "little",
      "frame": [
        { "name": "preamble", "role": "fixed", "width": 16, "value": ["0xAA","0x55"] },
        { "name": "len", "role": "length", "width": 16, "byteOrder": "little", "counts": ["flags.type","flags.subtype","seq"] },
        { "name": "cmd", "role": "value", "width": 8 },
        { "name": "status.type", "role": "value", "width": 4, "default": "0x1" },
        { "name": "status.subtype", "role": "value", "width": 4 },
        { "name": "flags.type", "role": "value", "width": 4 },
        { "name": "flags.subtype", "role": "value", "width": 4 },
        { "name": "seq", "role": "value", "width": 8 },
        { "name": "crc", "role": "checksum", "width": 16, "byteOrder": "little", "algo": "crc16",
          "over": { "from": "preamble", "to": "seq" },
          "params": { "poly":"0x1021","init":"0xFFFF","refIn":false,"refOut":false,"xorOut":"0x0000" } }
      ],
      "commands": [ { "name": "setFlags", "title": "Set Flags", "values": { "cmd": "0x10" } } ]
    }
    """;

    [Fact]
    public void Demo_bits_loads_packs_and_parses_through_schema()
    {
        var def = SegLoader.Load(DemoBitsJson);
        var engine = new FrameEngine(def.Frame, def.DefaultByteOrder);
        var values = new Dictionary<string, object>
        {
            ["cmd"] = 0x10, ["flags.type"] = 0x5, ["flags.subtype"] = 0x3, ["seq"] = 0x7,
        };
        var frame = engine.Pack(values);
        Assert.Equal(0x02, frame[2]);   // len = 2 (flags byte + seq byte)
        Assert.Equal(0x10, frame[5]);   // status.type=1<<4 | subtype=0
        Assert.Equal(0x53, frame[6]);   // flags.type=5<<4 | subtype=3
        Assert.Equal(0x07, frame[7]);   // seq
        var decoded = engine.Parse(frame);
        Assert.Null(decoded.Error);
        // round-trip through saver too
        var again = SegLoader.Load(SegSaver.ToJson(def));
        Assert.Equal(9, again.Frame.Length);
    }
}
