using System.IO.Ports;

namespace SerialForge.Transport;

public sealed record SerialTransportOptions(
    string PortName, int BaudRate, int DataBits = 8, Parity Parity = Parity.None,
    StopBits StopBits = StopBits.One, Handshake Handshake = Handshake.None);

public sealed class SerialTransport : ITransport
{
    private readonly SerialTransportOptions _opt;
    private SerialPort? _port;
    private Thread? _readThread;
    private CancellationTokenSource? _cts;

    public bool IsOpen => _port?.IsOpen == true;
    public event EventHandler<byte[]>? BytesReceived;

    public SerialTransport(SerialTransportOptions opt) => _opt = opt;

    public void Open()
    {
        if (IsOpen) return;
        _port = new SerialPort(_opt.PortName, _opt.BaudRate, _opt.Parity, _opt.DataBits, _opt.StopBits)
        { Handshake = _opt.Handshake, ReadBufferSize = 1 << 16 };
        _port.Open();
        _cts = new CancellationTokenSource();
        _readThread = new Thread(() => ReadLoop(_cts.Token)) { IsBackground = true };
        _readThread.Start();
    }

    private void ReadLoop(CancellationToken ct)
    {
        var buf = new byte[4096];
        while (!ct.IsCancellationRequested && _port is { IsOpen: true })
        {
            try
            {
                int n = _port.Read(buf, 0, buf.Length);
                if (n > 0) BytesReceived?.Invoke(this, buf[..n]);
            }
            catch (IOException) { break; }
            catch (InvalidOperationException) { break; }
        }
    }

    public void Write(byte[] data)
    {
        if (!IsOpen) throw new InvalidOperationException("transport not open");
        _port!.Write(data, 0, data.Length);
    }

    public void Close()
    {
        _cts?.Cancel();
        // Wait for the read thread to observe cancellation and exit before
        // closing/disposing the port — otherwise the new SerialPort created on a
        // reconnect can race the still-running reader on the same COM port.
        _readThread?.Join(1000);
        _port?.Close();
    }

    public void Dispose()
    {
        Close();
        _port?.Dispose();
    }
}
