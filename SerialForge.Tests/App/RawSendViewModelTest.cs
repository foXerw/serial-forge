using SerialForge.App.ViewModels;

namespace SerialForge.Tests.App;

public class RawSendViewModelTest
{
    [Fact]
    public void Send_parses_hex_and_invokes_sender_with_bytes()
    {
        byte[]? sent = null;
        var vm = new RawSendViewModel(b => sent = b);
        vm.Input = "AA 55 0x10";
        vm.Send.Execute(null);
        Assert.Equal(new byte[] { 0xAA, 0x55, 0x10 }, sent);
    }

    [Fact]
    public void Invalid_hex_surfaces_error_and_does_not_send()
    {
        byte[]? sent = null;
        string? error = null;
        var vm = new RawSendViewModel(b => sent = b, msg => error = msg);
        vm.Input = "GG";
        vm.Send.Execute(null);
        Assert.Null(sent);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Empty_input_does_not_send()
    {
        byte[]? sent = null;
        var vm = new RawSendViewModel(b => sent = b);
        vm.Input = "   ";
        vm.Send.Execute(null);
        Assert.Null(sent);
    }

    [Fact]
    public void Send_text_mode_encodes_utf8_and_invokes_sender()
    {
        byte[]? sent = null;
        var vm = new RawSendViewModel(b => sent = b) { Mode = RawSendMode.Text, Encoding = TextEncoding.Utf8 };
        vm.Input = "AB";
        vm.Send.Execute(null);
        Assert.Equal(new byte[] { 0x41, 0x42 }, sent);
    }

    [Fact]
    public void Send_text_mode_encodes_gbk()
    {
        byte[]? sent = null;
        var vm = new RawSendViewModel(b => sent = b) { Mode = RawSendMode.Text, Encoding = TextEncoding.Gbk };
        vm.Input = "A";
        vm.Send.Execute(null);
        Assert.NotNull(sent);
        Assert.Equal(new byte[] { 0x41 }, sent);   // ASCII subset identical in GBK
    }
}
