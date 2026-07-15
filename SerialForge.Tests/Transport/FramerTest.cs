using SerialForge.Core.Engine;
using SerialForge.Core.SegmentModel;
using SerialForge.Transport;
using SegLoader = SerialForge.Core.SegmentModel.ProtocolLoader;

namespace SerialForge.Tests.Transport;

public class FramerTest
{
    private static ProtocolDefinition Def() =>
        SegLoader.Load(File.ReadAllText("Fixtures/demo-mcu.json"));

    private static byte[] MakeFrame()
    {
        var def = Def();
        var cmd = def.Commands[0];
        var values = new Dictionary<string, object>();
        foreach (var kv in cmd.Values) values[kv.Key] = kv.Value;
        return new FrameEngine(def.Frame, def.DefaultByteOrder).Pack(values, cmd.Payload);
    }

    [Fact]
    public void LengthPrefix_frames_byte_at_a_time()
    {
        var framer = new Framer(Def());
        var frame = MakeFrame();
        var produced = new List<byte[]>();
        framer.FrameReady += (_, b) => produced.Add(b);

        foreach (var b in frame) framer.Feed(new[] { b });
        Assert.Single(produced);
        Assert.Equal(frame, produced[0]);
    }

    [Fact]
    public void LengthPrefix_handles_two_back_to_back_frames()
    {
        var framer = new Framer(Def());
        var f = MakeFrame();
        var produced = new List<byte[]>();
        framer.FrameReady += (_, b) => produced.Add(b);
        framer.Feed(f.Concat(f).ToArray());
        Assert.Equal(2, produced.Count);
    }
}
