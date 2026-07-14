using System.Text.Json;

namespace SerialForge.Core.Models;

// One ordered piece of a frame. Its bit offset in the frame = the sum of all
// preceding segments' bit widths; bits are packed MSB-first (bit 0 of a byte is
// its most-significant bit, so a Width=4 segment at offset 0 is the high nibble).
//
// role decides which fields apply:
//   Fixed    -> Value: hex byte literal (e.g. ["0xAA","0x55"]); Width must equal bytes*8.
//   Value    -> fixed Width: Default is an integer string fitting Width, optional Enum.
//               Width == null: the single variable payload segment; Default is hex bytes.
//   Length   -> Counts: names of byte-multiple (or the variable) segments whose byte
//               widths sum (+ Offset) into this field's value.
//   Checksum -> Algo (crc16|crc32|sum8|xor8) + OverFrom..OverTo inclusive range + Params.
//
// ByteOrder only affects segments with Width >= 16 that is a multiple of 8.
public sealed record Segment(
    string Name,
    SegmentRole Role,
    int? Width,
    ByteOrder? ByteOrder,
    string[]? Value,                          // Fixed: hex bytes
    string? Default,                          // Value: integer string, or payload hex bytes
    Dictionary<string, string>? Enum,         // Value: numeric -> display name
    string[]? Counts,                         // Length
    int Offset,                               // Length
    string? Algo,                             // Checksum
    string? OverFrom,                         // Checksum
    string? OverTo,                           // Checksum
    Dictionary<string, JsonElement>? Params); // Checksum: poly/init/refIn/refOut/xorOut
