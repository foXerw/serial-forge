namespace SerialForge.Transport;

public interface ITransport : IDisposable
{
    bool IsOpen { get; }
    void Open();
    void Close();
    void Write(byte[] data);
    event EventHandler<byte[]>? BytesReceived;
}
