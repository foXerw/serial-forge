namespace SerialForge.Core.Models;

public sealed record FramingRule(
    FramingMode Mode,
    string[]? Preamble,
    string? LengthField,
    int FrameTimeoutMs,
    string[]? Start,   // delimiter mode
    string[]? End);    // delimiter mode
