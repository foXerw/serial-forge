using SerialForge.Transport;

namespace SerialForge.Tests.Transport;

public class InMemoryTransportTest
{
    [Fact]
    public void Written_bytes_appear_on_peer()
    {
        var (a, b) = InMemoryTransport.CreatePair();
        a.Open(); b.Open();
        byte[]? received = null;
        b.BytesReceived += (_, data) => received = data;
        a.Write(new byte[] { 0x01, 0x02 });
        Assert.Equal(new byte[] { 0x01, 0x02 }, received);
    }
}
