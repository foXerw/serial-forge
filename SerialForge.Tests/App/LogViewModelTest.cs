using SerialForge.App.ViewModels;
using SerialForge.Core.Models;

namespace SerialForge.Tests.App;

public class LogViewModelTest
{
    [Fact]
    public void AddTx_and_AddRx_append_entries_with_direction_and_hex()
    {
        var vm = new LogViewModel();
        vm.AddTx(new byte[] { 0xAA, 0x55 });
        vm.AddRx(new DecodedFrame(Array.Empty<DecodedField>(), new byte[] { 0x01, 0x02 }, null));

        Assert.Equal(2, vm.Entries.Count);
        Assert.Equal("TX", vm.Entries[0].Direction);
        Assert.Equal("AA 55", vm.Entries[0].Hex);
        Assert.Equal("RX", vm.Entries[1].Direction);
    }

    [Fact]
    public void Log_caps_entries_to_bound()
    {
        var vm = new LogViewModel(maxEntries: 100);
        for (int i = 0; i < 150; i++) vm.AddTx(new byte[] { (byte)i });
        Assert.Equal(100, vm.Entries.Count);
    }
}
