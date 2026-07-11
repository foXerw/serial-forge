using SerialForge.Transport;

namespace SerialForge.Tests.Transport;

public class SerialTransportTest
{
    [Fact]
    public void Write_before_open_throws()
    {
        using var t = new SerialTransport(new SerialTransportOptions("COM99", 115200));
        Assert.False(t.IsOpen);
        Assert.Throws<InvalidOperationException>(() => t.Write(new byte[] { 1 }));
    }

    [Fact]
    public void Handler_exception_does_not_propagate_from_dispatch()
    {
        // A buggy subscriber must not kill the read loop. RaiseBytesReceived is
        // the dispatch routine ReadLoop calls after each successful port.Read.
        using var t = new SerialTransport(new SerialTransportOptions("COM99", 115200));
        bool called = false;
        t.BytesReceived += (_, _) => { called = true; throw new FormatException("handler bug"); };

        var ex = Record.Exception(() => t.RaiseBytesReceived(new byte[] { 1, 2 }));

        Assert.Null(ex);
        Assert.True(called);
    }
}
