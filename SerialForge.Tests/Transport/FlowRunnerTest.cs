using SerialForge.Core.Engine;
using SerialForge.Core.Models;
using SerialForge.Transport;

namespace SerialForge.Tests.Transport;

public class FlowRunnerTest
{
    private static ProtocolDefinition Def() =>
        ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-upgrade.json"));

    // 假设备：收到任意帧就回一个 cmd=0x06 的 ACK。
    private static void AutoAck(InMemoryTransport device, ProtocolEngine engine, CommandDef ackCmd)
    {
        device.BytesReceived += (_, bytes) =>
        {
            var ack = engine.Encode(new CommandInstance { Command = ackCmd });
            device.Write(ack);
        };
    }

    // Wire the host transport's received bytes into the dispatcher (the real app
    // does this in MainViewModel; tests must wire it explicitly).
    private static void Wire(FrameDispatcher dispatcher, InMemoryTransport host)
        => host.BytesReceived += (_, bytes) => dispatcher.OnBytes(bytes);

    [Fact]
    public async Task SendExpect_returns_matching_frame()
    {
        var def = Def();
        var engine = new ProtocolEngine(def);
        var dispatcher = new FrameDispatcher(engine, _ => _());
        var (host, device) = InMemoryTransport.CreatePair();
        host.Open(); device.Open();
        Wire(dispatcher, host);
        AutoAck(device, engine, def.Commands.First(c => c.Name == "upgradeAck"));

        byte[]? sent = null;
        var runner = new FlowRunner(engine, dispatcher, b => { sent = b; host.Write(b); });
        var inst = new CommandInstance { Command = def.Commands.First(c => c.Name == "startUpgrade") };

        var ack = await runner.SendExpect(inst, f => f.Fields.Any(x => x.Name == "cmd" && (ulong)x.Value! == 0x06), 500, 2, default);
        Assert.NotNull(ack);
        Assert.NotNull(sent);
    }

    [Fact]
    public async Task SendExpect_retries_then_fails_on_no_ack()
    {
        var def = Def();
        var engine = new ProtocolEngine(def);
        var dispatcher = new FrameDispatcher(engine, _ => _());
        var (host, device) = InMemoryTransport.CreatePair();
        host.Open(); device.Open();
        Wire(dispatcher, host);
        // 假设备不回 ACK
        var runner = new FlowRunner(engine, dispatcher, b => host.Write(b));
        var inst = new CommandInstance { Command = def.Commands.First(c => c.Name == "startUpgrade") };

        await Assert.ThrowsAsync<TimeoutException>(() =>
            runner.SendExpect(inst, _ => true, 20, 1, default));   // 1 retry -> 2 attempts, both time out
    }

    [Fact]
    public async Task SendExpect_propagates_cancellation()
    {
        var def = Def();
        var engine = new ProtocolEngine(def);
        var dispatcher = new FrameDispatcher(engine, _ => _());
        var (host, device) = InMemoryTransport.CreatePair();
        host.Open(); device.Open();
        Wire(dispatcher, host);
        var runner = new FlowRunner(engine, dispatcher, b => host.Write(b));
        var inst = new CommandInstance { Command = def.Commands.First(c => c.Name == "startUpgrade") };
        using var cts = new CancellationTokenSource();
        var t = runner.SendExpect(inst, _ => true, 5000, 0, cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t);
    }
}
