using SerialForge.Core.Engine;
using SerialForge.Core.SegmentModel;
using SerialForge.Transport;
using SegLoader = SerialForge.Core.SegmentModel.ProtocolLoader;

namespace SerialForge.Tests.Transport;

public class UpgradeFlowTest
{
    private static ProtocolDefinition Def() =>
        SegLoader.Load(File.ReadAllText("Fixtures/demo-upgrade.json"));

    private static FirmwareImage Image(int size, int chunkSize = 64)
    {
        var path = Path.Combine(Path.GetTempPath(), "fw-" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, new byte[size]);
        return FirmwareImage.Load(path, chunkSize);
    }

    private static byte[] Encode(FrameEngine engine, CommandDef cmd, Dictionary<string, object>? extra = null)
    {
        var values = new Dictionary<string, object>();
        foreach (var kv in cmd.Values) values[kv.Key] = kv.Value;
        if (extra is not null) foreach (var kv in extra) values[kv.Key] = kv.Value;
        return engine.Pack(values, cmd.Payload);
    }

    // 假设备：解析收到的 cmd；对 start/end 回 ACK(seq=0)，对 transfer 回 ACK(seq=收到的 seq)。
    private static void SmartAck(InMemoryTransport device, FrameEngine engine, ProtocolDefinition def)
    {
        var ackCmd = def.Commands.First(c => c.Name == "upgradeAck");
        device.BytesReceived += (_, bytes) =>
        {
            var d = engine.Parse(bytes);
            var cmd = (ulong)d.Fields.First(f => f.Name == "cmd").Value!;
            var payload = (byte[])d.Fields.First(f => f.Name == "payload").Value!;
            if (cmd != 0x10 && cmd != 0x11 && cmd != 0x12) return;
            ushort seq = (cmd == 0x11) ? (ushort)(payload[0] | (payload[1] << 8)) : (ushort)0;
            device.Write(Encode(engine, ackCmd, new() { ["tag"] = (ulong)(cmd == 0x11 ? 1 : 0), ["seq"] = (ulong)seq }));
        };
    }

    private static (UpgradeFlow flow, InMemoryTransport device) Setup()
    {
        var def = Def();
        var engine = new FrameEngine(def.Frame, def.DefaultByteOrder);
        var dispatcher = new FrameDispatcher(engine, def, _ => _());
        var (host, device) = InMemoryTransport.CreatePair();
        host.Open(); device.Open();
        host.BytesReceived += (_, b) => dispatcher.OnBytes(b);
        SmartAck(device, engine, def);
        var runner = new FlowRunner(dispatcher, b => host.Write(b));
        return (new UpgradeFlow(runner, engine, def, 500, 2), device);
    }

    [Fact]
    public async Task Full_upgrade_completes_with_progress()
    {
        var (flow, _) = Setup();
        var img = Image(200);   // 200B / 64 = 4 blocks (ceil)

        var reports = new List<UpgradeProgress>();
        var status = await flow.RunAsync(img, new Progress<UpgradeProgress>(reports.Add), default);

        Assert.Equal(UpgradeStatus.Done, status);
        Assert.Contains(reports, r => r.SentBlocks == r.TotalBlocks && r.TotalBlocks == 4);
    }

    [Fact]
    public async Task Silent_device_yields_failed()
    {
        var def = Def();
        var engine = new FrameEngine(def.Frame, def.DefaultByteOrder);
        var dispatcher = new FrameDispatcher(engine, def, _ => _());
        var (host, device) = InMemoryTransport.CreatePair();
        host.Open(); device.Open();
        host.BytesReceived += (_, b) => dispatcher.OnBytes(b);
        var runner = new FlowRunner(dispatcher, b => host.Write(b));
        var flow = new UpgradeFlow(runner, engine, def, 20, 1);
        var img = Image(64);
        var status = await flow.RunAsync(img, null, default);
        Assert.Equal(UpgradeStatus.Failed, status);
    }

    [Fact]
    public async Task Cancel_yields_cancelled()
    {
        var (flow, _) = Setup();
        var img = Image(10_000);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var status = await flow.RunAsync(img, null, cts.Token);
        Assert.Equal(UpgradeStatus.Cancelled, status);
    }
}
