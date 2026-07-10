namespace SerialForge.Core.Models;

public sealed record CommandDef(
    string Name,
    string Title,
    Dictionary<string, string> Fix,
    PayloadFieldDef[] PayloadFields);
