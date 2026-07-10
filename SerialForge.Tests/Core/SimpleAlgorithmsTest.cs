using SerialForge.Core;
using SerialForge.Core.Algorithms;
using SerialForge.Core.Models;

namespace SerialForge.Tests.Core;

public class SimpleAlgorithmsTest
{
    private readonly AlgorithmRegistry _r = new();

    [Fact]
    public void Sum8_sums_range_bytes()
    {
        var spec = new ComputeSpec("sum8", null, 0, null, null, null);
        var outBytes = _r.Get("sum8").Compute(new byte[] { 0x01, 0x02, 0x03 }, spec);
        Assert.Equal(new byte[] { 0x06 }, outBytes);
    }

    [Fact]
    public void Xor8_xor_range_bytes()
    {
        var spec = new ComputeSpec("xor8", null, 0, null, null, null);
        var outBytes = _r.Get("xor8").Compute(new byte[] { 0xFF, 0x0F, 0xF0 }, spec);
        Assert.Equal(new byte[] { 0x00 }, outBytes);
    }
}
