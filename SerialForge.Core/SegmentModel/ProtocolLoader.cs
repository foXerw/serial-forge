using System.Text.Json;
using SerialForge.Core;
using SerialForge.Core.Codecs;
using SerialForge.Core.Exceptions;
using SerialForge.Core.Models;

namespace SerialForge.Core.SegmentModel;

public static class ProtocolLoader
{
    private static readonly JsonSerializerOptions Opt = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true
    };

    public static ProtocolDefinition Load(string json)
    {
        Dto.ProtocolDto dto;
        try { dto = JsonSerializer.Deserialize<Dto.ProtocolDto>(json, Opt) ?? throw new ProtocolException("empty protocol"); }
        catch (JsonException ex) { throw new ProtocolException("invalid JSON: " + ex.Message, ex); }

        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ProtocolException("missing 'name' in protocol");
        if (string.IsNullOrWhiteSpace(dto.Version)) throw new ProtocolException("missing 'version' in protocol");
        if (dto.Frame is null || dto.Frame.Length == 0) throw new ProtocolException("missing 'frame' in protocol");
        if (dto.Commands is null) throw new ProtocolException("missing 'commands' in protocol");

        var order = dto.DefaultByteOrder == "big" ? ByteOrder.Big : ByteOrder.Little;
        var frame = dto.Frame!.Select(ToSegment).ToArray();
        var commands = dto.Commands!.Select(ToCommand).ToArray();
        var def = new ProtocolDefinition(dto.Name!, dto.Version!, order, frame, commands);
        Validate(def);
        return def;
    }

    public static ProtocolDefinition LoadFile(string path) => Load(File.ReadAllText(path));

    private static Segment ToSegment(Dto.SegmentDto s) => new(
        s.Name!, ParseRole(s.Role!), s.Width, ParseOrder(s.ByteOrder),
        s.Value, s.Default, s.Enum, s.Counts, s.Offset ?? 0, s.Algo,
        s.Over?.From, s.Over?.To, s.Params);

    private static CommandDef ToCommand(Dto.CommandDto c) => new(
        c.Name!, c.Title ?? c.Name!, c.Values ?? new());

    private static SegmentRole ParseRole(string s) => s.ToLowerInvariant() switch
    {
        "fixed" => SegmentRole.Fixed,
        "value" => SegmentRole.Value,
        "length" => SegmentRole.Length,
        "checksum" => SegmentRole.Checksum,
        _ => throw new ProtocolException($"unknown role '{s}'")
    };

    private static ByteOrder? ParseOrder(string? s) => s switch
    {
        "big" => ByteOrder.Big,
        "little" => ByteOrder.Little,
        _ => null
    };

    public static void Validate(ProtocolDefinition def)
    {
        if (string.IsNullOrWhiteSpace(def.Name)) throw new ProtocolException("missing 'name' in protocol");
        if (def.Frame is null || def.Frame.Length == 0) throw new ProtocolException("missing 'frame' in protocol");
        if (def.Commands is null) throw new ProtocolException("missing 'commands' in protocol");

        var names = new HashSet<string>();
        foreach (var s in def.Frame)
            if (!names.Add(s.Name)) throw new ProtocolException($"duplicate segment name '{s.Name}'");

        int variable = -1;
        for (int i = 0; i < def.Frame.Length; i++)
        {
            if (def.Frame[i].Width is null)
            {
                if (variable >= 0) throw new ProtocolException("at most one variable-width segment allowed");
                if (def.Frame[i].Role != SegmentRole.Value)
                    throw new ProtocolException($"variable segment '{def.Frame[i].Name}' must have role 'value'");
                variable = i;
            }
        }
        if (variable >= 0)
        {
            int before = 0;
            for (int i = 0; i < variable; i++) before += def.Frame[i].Width!.Value;
            if (before % 8 != 0) throw new ProtocolException("variable segment must start on a byte boundary");
            int after = 0;
            for (int i = variable + 1; i < def.Frame.Length; i++) after += def.Frame[i].Width!.Value;
            if (after % 8 != 0) throw new ProtocolException("segments after the variable segment must total whole bytes");
        }

        int lengthCount = 0, lengthIndex = -1;
        for (int i = 0; i < def.Frame.Length; i++)
        {
            var s = def.Frame[i];
            switch (s.Role)
            {
                case SegmentRole.Fixed:
                    if (s.Width is not int fw || fw <= 0 || fw % 8 != 0)
                        throw new ProtocolException($"fixed segment '{s.Name}' width must be a positive multiple of 8");
                    var bytes = (s.Value ?? Array.Empty<string>()).SelectMany(BytesCodec.ParseHex).ToArray();
                    if (bytes.Length == 0) throw new ProtocolException($"fixed segment '{s.Name}' needs a value");
                    if (fw != bytes.Length * 8) throw new ProtocolException($"fixed segment '{s.Name}' width {fw} != value {bytes.Length} bytes");
                    break;
                case SegmentRole.Value when s.Width is int vw:
                    if (vw < 1) throw new ProtocolException($"value segment '{s.Name}' width must be >= 1");
                    if (s.Default is { Length: > 0 } d && ParseInt(d) >> vw != 0)
                        throw new ProtocolException($"value segment '{s.Name}' default overflows {vw} bits");
                    break;
                case SegmentRole.Length:
                    lengthCount++; lengthIndex = i;
                    if (s.Width is not int lw || lw < 1) throw new ProtocolException($"length segment '{s.Name}' width must be >= 1");
                    if (s.Counts is null || s.Counts.Length == 0) throw new ProtocolException($"length segment '{s.Name}' needs counts");
                    foreach (var c in s.Counts)
                        if (!names.Contains(c)) throw new ProtocolException($"length '{s.Name}' counts unknown segment '{c}'");
                    break;
                case SegmentRole.Checksum:
                    if (s.Width is not int cw || cw < 8 || cw % 8 != 0)
                        throw new ProtocolException($"checksum segment '{s.Name}' width must be a positive multiple of 8");
                    if (string.IsNullOrWhiteSpace(s.Algo)) throw new ProtocolException($"checksum segment '{s.Name}' needs an algo");
                    if (s.OverFrom is null || s.OverTo is null) throw new ProtocolException($"checksum segment '{s.Name}' needs over.from/to");
                    if (!names.Contains(s.OverFrom) || !names.Contains(s.OverTo))
                        throw new ProtocolException($"checksum '{s.Name}' references unknown segment");
                    int cf = IndexOf(def.Frame, s.OverFrom), ct = IndexOf(def.Frame, s.OverTo);
                    if (i >= cf && i <= ct) throw new ProtocolException($"checksum '{s.Name}' cannot cover itself");
                    break;
            }
        }

        if (lengthCount > 1) throw new ProtocolException("at most one length segment allowed");
        if (variable >= 0 && lengthIndex >= 0
            && def.Frame[lengthIndex].Counts!.Contains(def.Frame[variable].Name)
            && lengthIndex > variable)
            throw new ProtocolException("length segment must precede the variable segment it counts");
    }

    private static int IndexOf(Segment[] frame, string name)
    {
        for (int i = 0; i < frame.Length; i++) if (frame[i].Name == name) return i;
        return -1;
    }

    private static ulong ParseInt(string s) => s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        ? Convert.ToUInt64(s[2..], 16) : Convert.ToUInt64(s, 10);

    #pragma warning disable CS0649
    private static class Dto
    {
        public sealed class ProtocolDto { public string? Name; public string? Version; public string? DefaultByteOrder; public SegmentDto[]? Frame; public CommandDto[]? Commands; }
        public sealed class SegmentDto
        {
            public string? Name; public string? Role; public int? Width; public string? ByteOrder;
            public string[]? Value; public string? Default; public Dictionary<string, string>? Enum;
            public string[]? Counts; public int? Offset; public string? Algo; public OverDto? Over;
            public Dictionary<string, JsonElement>? Params;
        }
        public sealed class OverDto { public string? From; public string? To; }
        public sealed class CommandDto { public string? Name; public string? Title; public Dictionary<string, string>? Values; }
    }
    #pragma warning restore CS0649
}
