using SerialForge.Core.Engine;
using SerialForge.Core.SegmentModel;
using SerialForge.Transport;

namespace SerialForge.App.Services;

// Real IUpgradeRunner: loads the firmware, builds a FlowRunner/UpgradeFlow against
// the live engine/dispatcher/transport, and runs it. TX is logged via the injected
// logger; RX flows through the existing dispatcher -> Log path.
public sealed class UpgradeRunner : IUpgradeRunner
{
    private readonly FrameEngine _engine;
    private readonly ProtocolDefinition _def;
    private readonly FrameDispatcher _dispatcher;
    private readonly ITransport _transport;
    private readonly Action<byte[]> _logTx;
    private readonly int _timeoutMs;
    private readonly int _retries;

    public UpgradeRunner(FrameEngine engine, ProtocolDefinition def, FrameDispatcher dispatcher, ITransport transport,
        Action<byte[]> logTx, int timeoutMs = 1000, int retries = 3)
    { _engine = engine; _def = def; _dispatcher = dispatcher; _transport = transport; _logTx = logTx; _timeoutMs = timeoutMs; _retries = retries; }

    public async Task<UpgradeStatus> RunAsync(string firmwarePath, int chunkSize, IProgress<UpgradeProgress> progress, CancellationToken ct)
    {
        var image = FirmwareImage.Load(firmwarePath, chunkSize);
        void Send(byte[] frame) { _logTx(frame); _transport.Write(frame); }
        var runner = new FlowRunner(_dispatcher, Send);
        var flow = new UpgradeFlow(runner, _engine, _def, _timeoutMs, _retries);
        return await flow.RunAsync(image, progress, ct);
    }
}
