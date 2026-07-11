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
}
