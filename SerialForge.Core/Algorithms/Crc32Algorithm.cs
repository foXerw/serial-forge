using SerialForge.Core.Models;

namespace SerialForge.Core.Algorithms;

public sealed class Crc32Algorithm : IComputeAlgorithm
{
    public byte[] Compute(byte[] range, ComputeSpec spec)
    {
        ulong poly = spec.Params.GetHex("poly", 0x04C11DB7);
        uint crc = (uint)spec.Params.GetHex("init", 0xFFFFFFFF);
        bool refIn = spec.Params.GetBool("refIn", true);
        bool refOut = spec.Params.GetBool("refOut", true);
        uint xorOut = (uint)spec.Params.GetHex("xorOut", 0xFFFFFFFF);

        for (int i = 0; i < range.Length; i++)
        {
            byte b = refIn ? Reverse8(range[i]) : range[i];
            crc ^= (uint)b << 24;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 0x80000000) != 0 ? ((crc << 1) ^ (uint)poly) : (crc << 1);
        }
        crc ^= xorOut;
        if (refOut) crc = Reverse32(crc);

        // little-endian canonical (matches the zlib/Ethernet CRC-32 wire convention);
        // engine applies any configured field byte order on top.
        return new byte[]
        {
            (byte)(crc & 0xFF),
            (byte)((crc >> 8) & 0xFF),
            (byte)((crc >> 16) & 0xFF),
            (byte)((crc >> 24) & 0xFF)
        };
    }

    private static byte Reverse8(byte b)
    {
        b = (byte)((b >> 4) | (b << 4));
        b = (byte)(((b & 0x33) << 2) | ((b & 0xCC) >> 2));
        b = (byte)(((b & 0x55) << 1) | ((b & 0xAA) >> 1));
        return b;
    }
    private static uint Reverse32(uint v)
    {
        v = ((v & 0xFFFF0000) >> 16) | ((v & 0x0000FFFF) << 16);
        v = ((v & 0xFF00FF00) >> 8) | ((v & 0x00FF00FF) << 8);
        return ((uint)(Reverse8((byte)(v >> 24)) << 24) | (uint)(Reverse8((byte)(v >> 16)) << 16)
                | (uint)(Reverse8((byte)(v >> 8)) << 8) | (uint)Reverse8((byte)(v & 0xFF)));
    }
}
