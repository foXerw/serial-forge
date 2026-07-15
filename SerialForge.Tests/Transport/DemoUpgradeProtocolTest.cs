using SerialForge.Core.Models;
using SegLoader = SerialForge.Core.SegmentModel.ProtocolLoader;

namespace SerialForge.Tests.Transport;

public class DemoUpgradeProtocolTest
{
    [Fact]
    public void Demo_upgrade_protocol_loads_and_has_upgrade_commands()
    {
        var def = SegLoader.Load(File.ReadAllText("Fixtures/demo-upgrade.json"));
        var names = def.Commands.Select(c => c.Name).ToArray();
        Assert.Contains("startUpgrade", names);
        Assert.Contains("transferBlock", names);
        Assert.Contains("endUpgrade", names);
        Assert.Contains("upgradeAck", names);
        var transfer = def.Commands.First(c => c.Name == "transferBlock");
        Assert.NotNull(transfer.Payload);
        var data = transfer.Payload!.First(p => p.Name == "data");
        Assert.Null(data.Width);   // variable payload segment
        Assert.Equal(SegmentRole.Value, data.Role);
    }
}
