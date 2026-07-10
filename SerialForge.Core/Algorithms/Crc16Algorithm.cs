using SerialForge.Core.Models;

namespace SerialForge.Core.Algorithms;

public sealed class Crc16Algorithm : IComputeAlgorithm
{
    public byte[] Compute(byte[] range, ComputeSpec spec)
    {
        ulong poly = spec.Params.GetHex("poly", 0x1021);
        ushort crc = (ushort)spec.Params.GetHex("init", 0xFFFF);
        bool refIn = spec.Params.GetBool("refIn", false);
        bool refOut = spec.Params.GetBool("refOut", false);
        ushort xorOut = (ushort)spec.Params.GetHex("xorOut", 0);

        for (int i = 0; i < range.Length; i++)
        {
            byte b = refIn ? Reverse8(range[i]) : range[i];
            crc ^= (ushort)(b << 8);
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ (ushort)poly) : (ushort)(crc << 1);
        }
        crc = (ushort)(crc ^ xorOut);
        if (refOut) crc = Reverse16(crc);

        // big-endian canonical; engine applies field byte order
        return new byte[] { (byte)(crc >> 8), (byte)(crc & 0xFF) };
    }

    private static byte Reverse8(byte b)
    {
        b = (byte)((b >> 4) | (b << 4));
        b = (byte)(((b & 0x33) << 2) | ((b & 0xCC) >> 2));
        b = (byte)(((b & 0x55) << 1) | ((b & 0xAA) >> 1));
        return b;
    }
    private static ushort Reverse16(ushort v) =>
        (ushort)((Reverse8((byte)(v >> 8)) << 8) | Reverse8((byte)(v & 0xFF)));
}
