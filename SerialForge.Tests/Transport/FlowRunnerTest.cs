using SerialForge.Core.Engine;
using SerialForge.Core.SegmentModel;
using SerialForge.Transport;
using SegLoader = SerialForge.Core.SegmentModel.ProtocolLoader;

namespace SerialForge.Tests.Transport;

public class FlowRunnerTest
{
    private static ProtocolDefinition Def() =>
        SegLoader.Load(File.ReadAllText("Fixtures/demo-upgrade.json"));

    private static byte[] Encode(FrameEngine engine, CommandDef cmd)
    {
        var values = new Dictionary<string, object>();
        foreach (var kv in cmd.Values) values[kv.Key] = kv.Value;
        return engine.Pack(values, cmd.Payload);
    }

    // 假设备：收到任意帧就回一个 cmd=0x06 的 ACK。
    private static void AutoAck(InMemoryTransport device, FrameEngine engine, CommandDef ackCmd)
    {
        device.BytesReceived += (_, bytes) => device.Write(Encode(engine, ackCmd));
    }

    private static void Wire(FrameDispatcher dispatcher, InMemoryTransport host)
        => host.BytesReceived += (_, bytes) => dispatcher.OnBytes(bytes);

    [Fact]
    public async Task SendExpect_returns_matching_frame()
    {
        var def = Def();
        var engine = new FrameEngine(def.Frame, def.DefaultByteOrder);
        var dispatcher = new FrameDispatcher(engine, def, _ => _());
        var (host, device) = InMemoryTransport.CreatePair();
        host.Open(); device.Open();
        Wire(dispatcher, host);
        AutoAck(device, engine, def.Commands.First(c => c.Name == "upgradeAck"));

        byte[]? sent = null;
        var runner = new FlowRunner(dispatcher, b => { sent = b; host.Write(b); });
        var frame = Encode(engine, def.Commands.First(c => c.Name == "startUpgrade"));

        var ack = await runner.SendExpect(frame, f => f.Fields.Any(x => x.Name == "cmd" && (ulong)x.Value! == 0x06), 500, 2, default);
        Assert.NotNull(ack);
        Assert.NotNull(sent);
    }

    [Fact]
    public async Task SendExpect_retries_then_fails_on_no_ack()
    {
        var def = Def();
        var engine = new FrameEngine(def.Frame, def.DefaultByteOrder);
        var dispatcher = new FrameDispatcher(engine, def, _ => _());
        var (host, device) = InMemoryTransport.CreatePair();
        host.Open(); device.Open();
        Wire(dispatcher, host);
        var runner = new FlowRunner(dispatcher, b => host.Write(b));
        var frame = Encode(engine, def.Commands.First(c => c.Name == "startUpgrade"));

        await Assert.ThrowsAsync<TimeoutException>(() =>
            runner.SendExpect(frame, _ => true, 20, 1, default));   // 1 retry -> 2 attempts, both time out
    }

    [Fact]
    public async Task SendExpect_propagates_cancellation()
    {
        var def = Def();
        var engine = new FrameEngine(def.Frame, def.DefaultByteOrder);
        var dispatcher = new FrameDispatcher(engine, def, _ => _());
        var (host, device) = InMemoryTransport.CreatePair();
        host.Open(); device.Open();
        Wire(dispatcher, host);
        var runner = new FlowRunner(dispatcher, b => host.Write(b));
        var frame = Encode(engine, def.Commands.First(c => c.Name == "startUpgrade"));
        using var cts = new CancellationTokenSource();
        var t = runner.SendExpect(frame, _ => true, 5000, 0, cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t);
    }
}
