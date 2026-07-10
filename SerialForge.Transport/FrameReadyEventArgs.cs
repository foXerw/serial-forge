namespace SerialForge.Transport;

public sealed class FrameReadyEventArgs : EventArgs
{
    public byte[] Frame { get; init; } = Array.Empty<byte>();
}
