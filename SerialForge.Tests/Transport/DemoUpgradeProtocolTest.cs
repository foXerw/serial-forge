using SerialForge.Core.Engine;

namespace SerialForge.Tests.Transport;

public class DemoUpgradeProtocolTest
{
    [Fact]
    public void Demo_upgrade_protocol_loads_and_has_upgrade_commands()
    {
        var def = ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-upgrade.json"));
        var names = def.Commands.Select(c => c.Name).ToArray();
        Assert.Contains("startUpgrade", names);
        Assert.Contains("transferBlock", names);
        Assert.Contains("endUpgrade", names);
        Assert.Contains("upgradeAck", names);
        var transfer = def.Commands.First(c => c.Name == "transferBlock");
        Assert.Equal(64, transfer.PayloadFields.First(p => p.Name == "data").Size);
    }
}
