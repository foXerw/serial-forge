namespace SerialForge.Core.Models;

public sealed record DecodedField(string Name, object? Value, int Offset, int Length, bool IsError);

public sealed record DecodedFrame(DecodedField[] Fields, byte[] Raw, string? Error);
