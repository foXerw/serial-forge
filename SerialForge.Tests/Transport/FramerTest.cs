using SerialForge.Core.Engine;
using SerialForge.Core.Models;
using SerialForge.Transport;

namespace SerialForge.Tests.Transport;

public class FramerTest
{
    private static ProtocolDefinition Def() =>
        ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-mcu.json"));

    private static byte[] MakeFrame() =>
        new ProtocolEngine(Def()).Encode(new CommandInstance { Command = Def().Commands[0] });

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
