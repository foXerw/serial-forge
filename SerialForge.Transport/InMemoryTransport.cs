namespace SerialForge.Transport;

public sealed class InMemoryTransport : ITransport
{
    private InMemoryTransport? _peer;
    public bool IsOpen { get; private set; }
    public event EventHandler<byte[]>? BytesReceived;

    public void Open() => IsOpen = true;
    public void Close() => IsOpen = false;

    public void Write(byte[] data)
    {
        if (!IsOpen) throw new InvalidOperationException("transport not open");
        _peer?.BytesReceived?.Invoke(_peer, data);
    }

    internal void SetPeer(InMemoryTransport peer) => _peer = peer;
    public void Dispose() => Close();

    public static (InMemoryTransport a, InMemoryTransport b) CreatePair()
    {
        var a = new InMemoryTransport();
        var b = new InMemoryTransport();
        a.SetPeer(b); b.SetPeer(a);
        return (a, b);
    }
}
