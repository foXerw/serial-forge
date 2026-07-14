using System.Text.Json;
using SerialForge.Core.Engine;
using SerialForge.Core.Models;

namespace SerialForge.Tests.Core;

public class ProtocolSaverTest
{
    private static ProtocolDefinition Def() =>
        ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-mcu.json"));

    [Fact]
    public void Roundtrip_load_save_load_is_equal()
    {
        var json = ProtocolSaver.ToJson(Def());
        var again = ProtocolLoader.Load(json);
        Assert.Equal(Def().Name, again.Name);
        Assert.Equal(Def().Layout.Length, again.Layout.Length);
        Assert.Equal(Def().Commands.Length, again.Commands.Length);
    }

    [Fact]
    public void Saved_json_uses_lowercase_0x_hex_in_crc_params()
    {
        var json = ProtocolSaver.ToJson(Def());
        using var doc = JsonDocument.Parse(json);
        var crc = System.Linq.Enumerable.First(doc.RootElement.GetProperty("layout").EnumerateArray(),
            f => f.GetProperty("name").GetString() == "crc16");
        var poly = crc.GetProperty("compute").GetProperty("params").GetProperty("poly").GetString();
        Assert.StartsWith("0x", poly);
        Assert.DoesNotContain("0X", poly);
    }

    [Fact]
    public void Saved_json_can_be_reloaded()
    {
        var json = ProtocolSaver.ToJson(Def());
        // 能重新 Load 即证明 schema 合法
        ProtocolLoader.Load(json);
    }

    [Fact]
    public void Saved_protocol_round_trips_and_encodes_length_field_without_throwing()
    {
        var reloaded = ProtocolLoader.Load(ProtocolSaver.ToJson(Def()));
        var engine = new ProtocolEngine(reloaded);
        var inst = new CommandInstance { Command = reloaded.Commands[0] }; // readVersion (zero payload)
        var frame = engine.Encode(inst);
        // width must survive as a number so len encodes as 2 bytes; golden from Phase 1.
        Assert.Equal(new byte[] { 0xAA, 0x55, 0x00, 0x00, 0x01, 0x99, 0xA4 }, frame);
    }

    [Fact]
    public void Bits_round_trip_through_saver()
    {
        var def = ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-bits.json"));
        var json = ProtocolSaver.ToJson(def);
        var again = ProtocolLoader.Load(json);
        var status = again.Layout.First(f => f.Name == "status");
        Assert.Equal(2, status.Bits!.Length);
        Assert.Equal("0x1", status.Bits[0].Default);
        Assert.NotNull(again.Commands[0].PayloadFields.First(p => p.Name == "flags").Bits);
    }

    [Fact]
    public void IsLength_round_trips_through_saver()
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
        var again = ProtocolLoader.Load(ProtocolSaver.ToJson(ProtocolLoader.Load(json)));
        var len = again.Layout.First(f => f.Name == "len");
        Assert.False(len.Bits!.First(b => b.Name == "ver").IsLength);
        Assert.True(len.Bits!.First(b => b.Name == "size").IsLength);
    }
}
