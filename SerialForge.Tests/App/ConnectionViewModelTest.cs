using SerialForge.App.ViewModels;
using SerialForge.Transport;

namespace SerialForge.Tests.App;

public class ConnectionViewModelTest
{
    [Fact]
    public void Connect_via_factory_sets_connected_status()
    {
        var vm = new ConnectionViewModel(_ => new InMemoryTransport());
        vm.PortName = "COM1"; vm.BaudRate = 115200;
        vm.Connect.Execute(null);
        Assert.Equal("已连接", vm.Status);
        Assert.True(vm.IsConnected);
    }

    [Fact]
    public void Disconnect_sets_disconnected_status()
    {
        var vm = new ConnectionViewModel(_ => new InMemoryTransport());
        vm.PortName = "COM1";
        vm.Connect.Execute(null);
        vm.Disconnect.Execute(null);
        Assert.Equal("未连接", vm.Status);
    }

    [Fact]
    public void Loads_port_and_baud_from_settings_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "s-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{\"portName\":\"COM9\",\"baudRate\":9600}");
        try
        {
            var vm = new ConnectionViewModel(_ => new InMemoryTransport(), path);
            Assert.Equal("COM9", vm.PortName);
            Assert.Equal(9600, vm.BaudRate);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Connect_persists_port_and_baud_to_settings_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "s-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var vm = new ConnectionViewModel(_ => new InMemoryTransport(), path);
            vm.PortName = "COM7";
            vm.BaudRate = 19200;
            vm.Connect.Execute(null);

            Assert.True(File.Exists(path));
            var reloaded = new ConnectionViewModel(_ => new InMemoryTransport(), path);
            Assert.Equal("COM7", reloaded.PortName);
            Assert.Equal(19200, reloaded.BaudRate);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
