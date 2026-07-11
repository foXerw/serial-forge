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

    [Fact]
    public void Entries_record_timestamp_from_injected_clock()
    {
        var stamp = new DateTime(2026, 7, 11, 12, 30, 45, 123);
        var vm = new LogViewModel(5000, () => stamp);
        vm.AddTx(new byte[] { 0xAA });
        Assert.Equal(stamp, vm.Entries[0].Timestamp);
    }

    [Fact]
    public void AddError_appends_marked_error_entry_with_message()
    {
        var vm = new LogViewModel();
        vm.AddError("编码失败：溢出");
        Assert.Single(vm.Entries);
        var e = vm.Entries[0];
        Assert.True(e.IsError);
        Assert.Equal("错误", e.Direction);
        Assert.Contains("编码失败", e.Detail);
    }
}
