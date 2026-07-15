using SerialForge.Core;
using SerialForge.Core.Models;

namespace SerialForge.Core.SegmentModel;

// A protocol is one ordered frame template (Segment[]) plus named command presets.
// This is the segment-model successor to the legacy Core.Models.ProtocolDefinition;
// during the migration both coexist in separate namespaces.
public sealed record ProtocolDefinition(
    string Name,
    string Version,
    ByteOrder DefaultByteOrder,
    Segment[] Frame,
    CommandDef[] Commands,
    int FrameTimeoutMs = 50);
