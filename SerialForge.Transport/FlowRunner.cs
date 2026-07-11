using SerialForge.Core.Engine;
using SerialForge.Core.Models;

namespace SerialForge.Transport;

// Minimal step primitive: encode + send one command, await a matching decoded
// frame, retry on timeout, propagate cancellation. Built on FrameDispatcher.Await.
public sealed class FlowRunner
{
    private readonly ProtocolEngine _engine;
    private readonly FrameDispatcher _dispatcher;
    private readonly Action<byte[]> _send;

    public FlowRunner(ProtocolEngine engine, FrameDispatcher dispatcher, Action<byte[]> send)
    { _engine = engine; _dispatcher = dispatcher; _send = send; }

    public async Task<DecodedFrame> SendExpect(CommandInstance inst, Func<DecodedFrame, bool> expect,
        int timeoutMs, int retries, CancellationToken ct)
    {
        var frame = _engine.Encode(inst);
        int attempts = retries + 1;
        for (int i = 0; i < attempts; i++)
        {
            ct.ThrowIfCancellationRequested();
            var task = _dispatcher.Await(expect, timeoutMs, ct);
            _send(frame);
            try { return await task; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (TimeoutException) when (i < attempts - 1) { /* retry */ }
        }
        throw new TimeoutException("send-expect retries exhausted");
    }
}
