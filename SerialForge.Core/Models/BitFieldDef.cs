namespace SerialForge.Core.Models;

// One sub-field packed inside a 1-byte (u8) bitfield group. Offset is from the
// MOST-significant bit (0 == bit7, so "前四位" is Offset=0,Width=4). Width in bits.
// Offset+Width must be <= 8. Enum maps a numeric value -> display name on decode.
public sealed record BitFieldDef(
    string Name,
    int Offset,
    int Width,
    Dictionary<string, string>? Enum,
    string? Default);
