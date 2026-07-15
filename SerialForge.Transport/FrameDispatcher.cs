using SerialForge.Core.Engine;
using SerialForge.Core.Models;
using SerialForge.Core.SegmentModel;

namespace SerialForge.Transport;

// Owns a Framer (built from the protocol's segment template) and parses each
// framed byte[] via the engine. Decoded frames are marshaled to a UI thread
// through the injected Action<Action> before FrameDecoded is raised. Await is
// the request/response seam: a Task<DecodedFrame> that completes when a matching
// frame arrives, times out, or is cancelled.
public sealed class FrameDispatcher
{
    private readonly FrameEngine _engine;
    private readonly Action<Action> _marshal;
    private readonly Framer _framer;
    private readonly List<Waiter> _waiters = new();

    public event EventHandler<DecodedFrame>? FrameDecoded;

    internal int WaiterCount => _waiters.Count;

    public FrameDispatcher(FrameEngine engine, ProtocolDefinition def, Action<Action> marshal)
    {
        _engine = engine;
        _marshal = marshal;
        _framer = new Framer(def);
        _framer.FrameReady += (_, bytes) => OnFramed(bytes);
    }

    public void OnBytes(byte[] chunk) => _framer.Feed(chunk);

    public void Tick() => _framer.Tick();

    private void OnFramed(byte[] frame)
    {
        var decoded = _engine.Parse(frame);
        _marshal(() =>
        {
            FrameDecoded?.Invoke(this, decoded);
            for (int i = _waiters.Count - 1; i >= 0; i--)
            {
                var w = _waiters[i];
                if (w.Pred(decoded))
                {
                    w.Tcs.TrySetResult(decoded);
                    w.Linked.Cancel();
                    _waiters.RemoveAt(i);
                }
            }
        });
    }

    public Task<DecodedFrame> Await(Func<DecodedFrame, bool> pred, int timeoutMs, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<DecodedFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeoutMs);
        var waiter = new Waiter(pred, tcs, linked);
        _marshal(() => _waiters.Add(waiter));
        linked.Token.Register(() =>
        {
            if (tcs.Task.IsCompleted) { linked.Dispose(); return; }
            if (ct.IsCancellationRequested) tcs.TrySetCanceled(ct);
            else tcs.TrySetException(new TimeoutException("await timed out"));
            _marshal(() => _waiters.Remove(waiter));
            linked.Dispose();
        });
        return tcs.Task;
    }

    private sealed class Waiter
    {
        public readonly Func<DecodedFrame, bool> Pred;
        public readonly TaskCompletionSource<DecodedFrame> Tcs;
        public readonly CancellationTokenSource Linked;
        public Waiter(Func<DecodedFrame, bool> pred, TaskCompletionSource<DecodedFrame> tcs, CancellationTokenSource linked)
        { Pred = pred; Tcs = tcs; Linked = linked; }
    }
}
