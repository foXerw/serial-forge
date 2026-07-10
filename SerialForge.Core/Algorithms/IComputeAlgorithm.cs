using SerialForge.Core.Models;

namespace SerialForge.Core.Algorithms;

public interface IComputeAlgorithm
{
    /// <summary>Compute the field's bytes over the given byte range.</summary>
    byte[] Compute(byte[] range, ComputeSpec spec);
}
