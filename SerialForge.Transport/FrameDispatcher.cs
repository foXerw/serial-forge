using SerialForge.Core.Engine;
using SerialForge.Core.Models;

namespace SerialForge.Transport;

// Owns a Framer (built from the engine's ProtocolDefinition) and decodes each
// framed byte[] via the engine. Decoded frames are marshaled to a UI thread
// through the injected Action<Action> before FrameDecoded is raised, so dispatch
// is testable on any thread (tests pass a no-op marshal) and safe on a reader
// thread in production. Await(predicate, timeout) is the Phase 2 seam: a
// Task<DecodedFrame> that completes when a matching frame arrives.
public sealed class FrameDispatcher
{
    private readonly ProtocolEngine _engine;
    private readonly Action<Action> _marshal;
    private readonly Framer _framer;
    private readonly List<(Func<DecodedFrame, bool> pred, TaskCompletionSource<DecodedFrame> tcs)> _waiters = new();

    public event EventHandler<DecodedFrame>? FrameDecoded;

    public FrameDispatcher(ProtocolEngine engine, Action<Action> marshal)
    {
        _engine = engine;
        _marshal = marshal;
        _framer = new Framer(engine.Definition);
        _framer.FrameReady += (_, bytes) => OnFramed(bytes);
    }

    /// <summary>Feed raw bytes from the transport.</summary>
    public void OnBytes(byte[] chunk) => _framer.Feed(chunk);

    private void OnFramed(byte[] frame)
    {
        var decoded = _engine.Decode(frame);
        _marshal(() =>
        {
            FrameDecoded?.Invoke(this, decoded);
            // Iterate backward so RemoveAt doesn't shift unvisited indices.
            for (int i = _waiters.Count - 1; i >= 0; i--)
            {
                if (_waiters[i].pred(decoded))
                {
                    _waiters[i].tcs.TrySetResult(decoded);
                    _waiters.RemoveAt(i);
                }
            }
        });
    }

    /// <summary>Phase 2 seam: resolve when a frame matching <paramref name="pred"/> arrives.</summary>
    public Task<DecodedFrame> Await(Func<DecodedFrame, bool> pred, int timeoutMs)
    {
        var tcs = new TaskCompletionSource<DecodedFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiters.Add((pred, tcs));
        _ = Task.Delay(timeoutMs).ContinueWith(_ => tcs.TrySetException(new TimeoutException("await timed out")));
        return tcs.Task;
    }
}
