using SerialForge.Core;
using SerialForge.Core.Models;

namespace SerialForge.Tests.Core;

public class ModelsTest
{
    [Fact]
    public void Records_construct_with_expected_defaults()
    {
        var spec = new ComputeSpec("crc16", null, 0, "preamble", "payload", null);
        Assert.Equal("crc16", spec.Algo);

        var field = new FieldDef("cmd", FieldKind.Value, CodecType.U8, null, null, null, null, null, null, null);
        Assert.Equal("cmd", field.Name);
        Assert.Equal(FieldKind.Value, field.Kind);

        var frame = new DecodedFrame(new DecodedField[] { new("cmd", 5, 0, 1, false) }, new byte[] { 5 }, null);
        Assert.Null(frame.Error);
    }
}
