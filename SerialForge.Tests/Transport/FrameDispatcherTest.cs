using SerialForge.Core.Engine;
using SerialForge.Core.Models;
using SerialForge.Core.SegmentModel;
using SerialForge.Transport;
using SegLoader = SerialForge.Core.SegmentModel.ProtocolLoader;

namespace SerialForge.Tests.Transport;

public class FrameDispatcherTest
{
    private static ProtocolDefinition Def() =>
        SegLoader.Load(File.ReadAllText("Fixtures/demo-mcu.json"));

    private static FrameEngine Engine(ProtocolDefinition def) => new(def.Frame, def.DefaultByteOrder);

    private static byte[] Encode(ProtocolDefinition def, FrameEngine engine)
    {
        var cmd = def.Commands[0];
        var values = new Dictionary<string, object>();
        foreach (var kv in cmd.Values) values[kv.Key] = kv.Value;
        return engine.Pack(values, cmd.Payload);
    }

    [Fact]
    public void Dispatches_decoded_frame_and_await_resolves()
    {
        var def = Def();
        var engine = Engine(def);
        var dispatcher = new FrameDispatcher(engine, def, _ => _());
        var frame = Encode(def, engine);

        DecodedFrame? seen = null;
        dispatcher.FrameDecoded += (_, f) => seen = f;

        var awaitTask = dispatcher.Await(f => f.Fields.Any(x => x.Name == "cmd" && (ulong)x.Value! == 0x01), 1000, default);
        dispatcher.OnBytes(frame);

        Assert.NotNull(seen);
        Assert.True(awaitTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Await_times_out_when_no_matching_frame()
    {
        var def = Def();
        var dispatcher = new FrameDispatcher(Engine(def), def, _ => _());
        await Assert.ThrowsAsync<TimeoutException>(() => dispatcher.Await(_ => false, 20, default));
        Assert.Equal(0, dispatcher.WaiterCount);
    }

    [Fact]
    public async Task Await_cancels_and_sweeps_waiter()
    {
        var def = Def();
        var dispatcher = new FrameDispatcher(Engine(def), def, _ => _());
        using var cts = new CancellationTokenSource();
        var task = dispatcher.Await(_ => false, 5000, cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(0, dispatcher.WaiterCount);
    }

    [Fact]
    public void Tick_flushes_idle_partial_frame_after_timeout()
    {
        var def = Def();
        var dispatcher = new FrameDispatcher(Engine(def), def, _ => _());
        DecodedFrame? seen = null;
        dispatcher.FrameDecoded += (_, f) => seen = f;

        // Preamble + one byte: not a complete length-prefix frame, so it sits buffered.
        dispatcher.OnBytes(new byte[] { 0xAA, 0x55, 0x99 });
        Assert.Null(seen);

        Thread.Sleep(70);
        dispatcher.Tick();
        Assert.NotNull(seen);
    }
}
