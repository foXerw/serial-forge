namespace SerialForge.Core.Engine;

// MSB-first bit access over a byte[]. Bit 0 of a byte is its most-significant
// bit, so a Width=4 field at bit offset 0 occupies the high nibble. Buffers are
// zero-initialized by callers, so Write only ever sets the 1-bits.
public static class BitOps
{
    public static ulong Read(byte[] data, int bitOffset, int width)
    {
        ulong v = 0;
        for (int i = 0; i < width; i++)
        {
            int bitIndex = bitOffset + i;
            int byteIndex = bitIndex >> 3;
            int bitInByte = 7 - (bitIndex & 7);
            v = (v << 1) | (uint)((data[byteIndex] >> bitInByte) & 1);
        }
        return v;
    }

    public static void Write(byte[] data, int bitOffset, int width, ulong value)
    {
        for (int i = 0; i < width; i++)
        {
            int shift = width - 1 - i;          // first written bit = value's MSB
            int bit = (int)((value >> shift) & 1);
            if (bit == 0) continue;
            int bitIndex = bitOffset + i;
            int byteIndex = bitIndex >> 3;
            int bitInByte = 7 - (bitIndex & 7);
            data[byteIndex] |= (byte)(1 << bitInByte);
        }
    }
}
