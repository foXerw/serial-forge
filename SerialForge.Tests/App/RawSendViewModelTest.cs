using SerialForge.App.ViewModels;

namespace SerialForge.Tests.App;

public class RawSendViewModelTest
{
    [Fact]
    public void Send_parses_hex_and_invokes_sender_with_bytes()
    {
        byte[]? sent = null;
        var vm = new RawSendViewModel(b => sent = b);
        vm.HexText = "AA 55 0x10";
        vm.Send.Execute(null);
        Assert.Equal(new byte[] { 0xAA, 0x55, 0x10 }, sent);
    }

    [Fact]
    public void Invalid_hex_surfaces_error_and_does_not_send()
    {
        byte[]? sent = null;
        string? error = null;
        var vm = new RawSendViewModel(b => sent = b, msg => error = msg);
        vm.HexText = "GG";
        vm.Send.Execute(null);
        Assert.Null(sent);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Empty_input_does_not_send()
    {
        byte[]? sent = null;
        var vm = new RawSendViewModel(b => sent = b);
        vm.HexText = "   ";
        vm.Send.Execute(null);
        Assert.Null(sent);
    }
}
