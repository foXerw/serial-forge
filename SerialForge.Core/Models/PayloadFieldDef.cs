namespace SerialForge.Core.Models;

public sealed record PayloadFieldDef(
    string Name,
    CodecType Codec,
    ByteOrder? ByteOrder,
    int? Size,
    string? Default);
