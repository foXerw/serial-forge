namespace SerialForge.Core.Models;

public sealed class CommandInstance
{
    public required CommandDef Command { get; init; }
    // Outer value-field inputs keyed by FieldDef.Name (hex strings or numbers).
    public Dictionary<string, object> FieldValues { get; set; } = new();
    // Payload sub-field inputs keyed by PayloadFieldDef.Name.
    public Dictionary<string, object> PayloadValues { get; set; } = new();
}
