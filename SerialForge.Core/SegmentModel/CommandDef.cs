namespace SerialForge.Core.SegmentModel;

// A named command is a thin preset: it fills some Value segments (e.g. cmd=0x05).
// Payload sub-fields, if any, are ordinary Value segments in the frame template.
public sealed record CommandDef(
    string Name,
    string Title,
    Dictionary<string, string> Values);
