namespace SerialForge.Core.Models;

public sealed record ProtocolDefinition(
    string Name,
    string Version,
    ByteOrder DefaultByteOrder,
    FramingRule Framing,
    FieldDef[] Layout,
    CommandDef[] Commands);
