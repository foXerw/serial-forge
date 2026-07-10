using System.Text.Json;

namespace SerialForge.Core.Models;

public sealed record ComputeSpec(
    string Algo,
    string[]? Counts,
    int Offset,
    string? From,
    string? To,
    Dictionary<string, JsonElement>? Params);
