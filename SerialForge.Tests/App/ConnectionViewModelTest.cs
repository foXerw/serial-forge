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
        Assert.Equal("Connected", vm.Status);
        Assert.True(vm.IsConnected);
    }

    [Fact]
    public void Disconnect_sets_disconnected_status()
    {
        var vm = new ConnectionViewModel(_ => new InMemoryTransport());
        vm.PortName = "COM1";
        vm.Connect.Execute(null);
        vm.Disconnect.Execute(null);
        Assert.Equal("Disconnected", vm.Status);
    }
}
