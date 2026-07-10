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

        var awaitTask = dispatcher.Await(f => f.Fields.Any(x => x.Name == "cmd" && (ulong)x.Value! == 0x01), 1000);
        dispatcher.OnBytes(frame);

        Assert.NotNull(seen);
        Assert.True(awaitTask.IsCompletedSuccessfully);
    }
}
