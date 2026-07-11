using SerialForge.Core.Engine;
using SerialForge.Core.Models;

namespace SerialForge.Transport;

// Owns a Framer (built from the engine's ProtocolDefinition) and decodes each
// framed byte[] via the engine. Decoded frames are marshaled to a UI thread
// through the injected Action<Action> before FrameDecoded is raised, so dispatch
// is testable on any thread (tests pass a no-op marshal) and safe on a reader
// thread in production. Await(predicate, timeout, ct) is the Phase 2 seam: a
// Task<DecodedFrame> that completes when a matching frame arrives, times out, or
// is cancelled — hardened so waiter registration, resolution, timeout, and
// cancellation all touch _waiters on the marshal thread with no residue.
public sealed class FrameDispatcher
{
    private readonly ProtocolEngine _engine;
    private readonly Action<Action> _marshal;
    private readonly Framer _framer;
    private readonly List<Waiter> _waiters = new();

    public event EventHandler<DecodedFrame>? FrameDecoded;

    // Test hook (InternalsVisibleTo): confirm timeout/cancel/match all sweep their entry.
    internal int WaiterCount => _waiters.Count;

    public FrameDispatcher(ProtocolEngine engine, Action<Action> marshal)
    {
        _engine = engine;
        _marshal = marshal;
        _framer = new Framer(engine.Definition);
        _framer.FrameReady += (_, bytes) => OnFramed(bytes);
    }

    /// <summary>Feed raw bytes from the transport.</summary>
    public void OnBytes(byte[] chunk) => _framer.Feed(chunk);

    /// <summary>Flush any partial frame that has sat idle past the protocol's
    /// frame timeout (Timeout/delimiter framing, or a stalled length-prefix read).
    /// Production wires this to a periodic UI-thread timer; tests call it directly.</summary>
    public void Tick() => _framer.Tick();

    private void OnFramed(byte[] frame)
    {
        var decoded = _engine.Decode(frame);
        _marshal(() =>
        {
            FrameDecoded?.Invoke(this, decoded);
            // Iterate backward so RemoveAt doesn't shift unvisited indices.
            for (int i = _waiters.Count - 1; i >= 0; i--)
            {
                var w = _waiters[i];
                if (w.Pred(decoded))
                {
                    w.Tcs.TrySetResult(decoded);
                    w.Linked.Cancel();   // stop the timeout timer; Register no-ops on the completed TCS
                    _waiters.RemoveAt(i);
                }
            }
        });
    }

    /// <summary>Phase 2 seam: resolve when a frame matching <paramref name="pred"/> arrives.</summary>
    public Task<DecodedFrame> Await(Func<DecodedFrame, bool> pred, int timeoutMs, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<DecodedFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeoutMs);
        var waiter = new Waiter(pred, tcs, linked);
        _marshal(() => _waiters.Add(waiter));   // register on the same thread that iterates _waiters
        linked.Token.Register(() =>
        {
            if (tcs.Task.IsCompleted) { linked.Dispose(); return; }   // matched first
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
