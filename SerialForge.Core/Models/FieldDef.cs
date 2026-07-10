namespace SerialForge.Core.Models;

public sealed record FieldDef(
    string Name,
    FieldKind Kind,
    CodecType Codec,
    ByteOrder? ByteOrder,
    int? Size,
    string[]? LiteralValue,   // for kind=Literal
    string? Default,          // for kind=Value
    Dictionary<string, string>? EnumMap,
    ComputeSpec? Compute);
