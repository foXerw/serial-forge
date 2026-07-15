namespace SerialForge.Core.Codecs;

// Hex byte parsing helper used by the segment loader/engine.
public static class BytesCodec
{
    public static byte[] ParseHex(string s)
    {
        var clean = s.Replace(" ", "").Replace("0x", "");
        if (clean.Length % 2 != 0) throw new ArgumentException("odd-length hex");
        var bytes = new byte[clean.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = byte.Parse(clean.AsSpan(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
        return bytes;
    }
}
