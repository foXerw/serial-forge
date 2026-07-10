using System.Text;

namespace SerialForge.Core.Codecs;

public sealed class StringCodec : ICodec
{
    public int? FixedSize => null;
    public byte[] Encode(object value, int length, ByteOrder order)
    {
        var s = value?.ToString() ?? "";
        var bytes = Encoding.ASCII.GetBytes(s);
        if (bytes.Length > length) bytes = bytes.Take(length).ToArray();
        if (bytes.Length < length)
        {
            var padded = new byte[length];
            Array.Copy(bytes, padded, bytes.Length);
            bytes = padded; // NUL pad
        }
        return bytes;
    }
    public (object? Value, int Consumed) Decode(byte[] data, int offset, int length, ByteOrder order)
    {
        var str = Encoding.ASCII.GetString(data, offset, length).TrimEnd('\0');
        return (str, length);
    }
}
