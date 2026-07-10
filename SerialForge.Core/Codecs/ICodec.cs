namespace SerialForge.Core.Codecs;

public interface ICodec
{
    /// <summary>Fixed byte size, or null when variable-length.</summary>
    int? FixedSize { get; }

    /// <param name="length">Resolved byte length for variable codecs; ignored by fixed numeric codecs.</param>
    byte[] Encode(object value, int length, ByteOrder order);

    /// <summary>Decode starting at <paramref name="offset"/>, consuming <paramref name="length"/> bytes.</summary>
    (object? Value, int Consumed) Decode(byte[] data, int offset, int length, ByteOrder order);
}
