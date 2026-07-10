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
}
