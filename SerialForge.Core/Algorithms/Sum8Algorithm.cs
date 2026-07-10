using SerialForge.Core.Models;

namespace SerialForge.Core.Algorithms;

public sealed class Sum8Algorithm : IComputeAlgorithm
{
    public byte[] Compute(byte[] range, ComputeSpec spec)
    {
        byte sum = 0;
        foreach (var b in range) sum += b;
        return new byte[] { sum };
    }
}
