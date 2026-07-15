using SerialForge.Core.Models;

namespace SerialForge.Core.SegmentModel;

// A named command is a thin preset: it fills some Value segments (e.g. cmd=0x05)
// via Values. Payload optionally structures the frame's variable payload segment
// as its own sub-template (a Segment[]), so different commands can lay their
// payloads out differently using the same segment abstraction.
public sealed record CommandDef(
    string Name,
    string Title,
    Dictionary<string, string> Values,
    Segment[]? Payload);
