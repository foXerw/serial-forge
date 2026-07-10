using SerialForge.Core.Models;

namespace SerialForge.Core.Algorithms;

public sealed class Xor8Algorithm : IComputeAlgorithm
{
    public byte[] Compute(byte[] range, ComputeSpec spec)
    {
        byte x = 0;
        foreach (var b in range) x ^= b;
        return new byte[] { x };
    }
}
