using SerialForge.Core.Engine;
using SerialForge.Core.Models;
using SerialForge.Transport;

namespace SerialForge.Tests.Transport;

public class FrameDispatcherTest
{
    private static ProtocolDefinition Def() =>
        ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-mcu.json"));

    [Fact]
    public void Dispatches_decoded_frame_and_await_resolves()
    {
        var def = Def();
        var engine = new ProtocolEngine(def);
        var dispatcher = new FrameDispatcher(engine, _ => _());
        var frame = engine.Encode(new CommandInstance { Command = def.Commands[0] });

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
        var dispatcher = new FrameDispatcher(new ProtocolEngine(Def()), _ => _());
        await Assert.ThrowsAsync<TimeoutException>(() => dispatcher.Await(_ => false, 20, default));
        Assert.Equal(0, dispatcher.WaiterCount);   // 死条已清扫
    }

    [Fact]
    public async Task Await_cancels_and_sweeps_waiter()
    {
        var dispatcher = new FrameDispatcher(new ProtocolEngine(Def()), _ => _());
        using var cts = new CancellationTokenSource();
        var task = dispatcher.Await(_ => false, 5000, cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(0, dispatcher.WaiterCount);
    }
}
