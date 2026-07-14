namespace SerialForge.Core.Models;

// One sub-field packed inside a 1-byte (u8) bitfield group. Offset is from the
// MOST-significant bit (0 == bit7, so "前四位" is Offset=0,Width=4). Width in bits.
// Offset+Width must be <= 8. Enum maps a numeric value -> display name on decode.
//
// IsLength marks the child that carries an auto-computed length value when the
// owning field is a Computed length bitfield (e.g. a byte shared between a
// version field and the payload length). Exactly one child may set it, and only
// on a Computed+length owner; ignored (must be false) on Value/payload bitfields.
public sealed record BitFieldDef(
    string Name,
    int Offset,
    int Width,
    Dictionary<string, string>? Enum,
    string? Default,
    bool IsLength = false);
