using SerialForge.Core.Models;

namespace SerialForge.Transport;

// Minimal step primitive: send one pre-encoded frame, await a matching decoded
// frame, retry on timeout, propagate cancellation. The caller encodes (the frame
// bytes are passed in); this only owns the send + await/retry loop.
public sealed class FlowRunner
{
    private readonly FrameDispatcher _dispatcher;
    private readonly Action<byte[]> _send;

    public FlowRunner(FrameDispatcher dispatcher, Action<byte[]> send)
    { _dispatcher = dispatcher; _send = send; }

    public async Task<DecodedFrame> SendExpect(byte[] frame, Func<DecodedFrame, bool> expect,
        int timeoutMs, int retries, CancellationToken ct)
    {
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
