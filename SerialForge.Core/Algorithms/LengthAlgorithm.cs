using System.Text.Json;
using SerialForge.Core.Models;

namespace SerialForge.Core.Algorithms;

/// <summary>
/// The "range" passed in is the concatenation of all counted fields' bytes;
/// LengthAlgorithm returns that count (plus offset) as a big-endian integer
/// of a width derived from Params.width (default 1). The engine re-applies
/// byte order when placing it, so we emit big-endian canonical bytes here.
/// </summary>
public sealed class LengthAlgorithm : IComputeAlgorithm
{
    public byte[] Compute(byte[] range, ComputeSpec spec)
    {
        int width = spec.Params?.TryGetValue("width", out var w) == true ? w.GetInt32() : 1;
        long value = range.Length + spec.Offset;
        var bytes = new byte[width];
        for (int i = 0; i < width; i++)
            bytes[width - 1 - i] = (byte)(value >> (8 * i));
        return bytes;
    }
}
