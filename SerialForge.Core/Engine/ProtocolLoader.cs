using System.Text.Json;
using SerialForge.Core.Exceptions;
using SerialForge.Core.Models;

namespace SerialForge.Core.Engine;

public static class ProtocolLoader
{
    // IncludeFields=true: the nested DTO classes use public FIELDS, which
    // System.Text.Json ignores by default. PropertyNameCaseInsensitive lets
    // us match camelCase JSON keys to the DTO field names.
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

        var order = dto.DefaultByteOrder == "big" ? ByteOrder.Big : ByteOrder.Little;
        var framing = new FramingRule(
            ParseEnum<FramingMode>(dto.Framing!.Mode),
            dto.Framing.Preamble,
            dto.Framing.LengthField,
            dto.Framing.FrameTimeoutMs,
            dto.Framing.Start,
            dto.Framing.End);

        var layout = dto.Layout!.Select(ToFieldDef).ToArray();
        var names = layout.Select(f => f.Name).ToHashSet();
        var commands = dto.Commands!.Select(ToCommandDef).ToArray();

        Validate(framing, layout, names);
        return new ProtocolDefinition(dto.Name!, dto.Version!, order, framing, layout, commands);
    }

    public static ProtocolDefinition LoadFile(string path) => Load(File.ReadAllText(path));

    private static FieldDef ToFieldDef(Dto.FieldDto f) => new(
        f.Name!, ParseEnum<FieldKind>(f.Kind!), ParseEnum<CodecType>(f.Codec!),
        f.ByteOrder == "big" ? ByteOrder.Big : f.ByteOrder == "little" ? ByteOrder.Little : null,
        f.Size, f.Value, f.Default, f.Enum, ToCompute(f.Compute));

    private static ComputeSpec? ToCompute(Dto.ComputeDto? c) => c is null ? null : new(
        c.Algo!, c.Counts, c.Offset ?? 0, c.Over?.From, c.Over?.To, c.Params);

    private static CommandDef ToCommandDef(Dto.CommandDto c) => new(
        c.Name!, c.Title ?? c.Name!, c.Fix ?? new(),
        (c.PayloadFields ?? Array.Empty<Dto.PayloadFieldDto>()).Select(p => new PayloadFieldDef(
            p.Name!, ParseEnum<CodecType>(p.Codec!),
            p.ByteOrder == "big" ? ByteOrder.Big : p.ByteOrder == "little" ? ByteOrder.Little : null,
            p.Size, p.Default)).ToArray());

    private static void Validate(FramingRule framing, FieldDef[] layout, HashSet<string> names)
    {
        if (framing.Mode == FramingMode.LengthPrefix)
        {
            if (framing.LengthField is null || !names.Contains(framing.LengthField))
                throw new ProtocolException($"framing.lengthField '{framing.LengthField}' not in layout");
        }
        foreach (var f in layout)
        {
            if (f.Kind == FieldKind.Computed && f.Compute is { } c)
            {
                if (c.From is not null && !names.Contains(c.From))
                    throw new ProtocolException($"compute.over.from '{c.From}' unknown (field {f.Name})");
                if (c.To is not null && !names.Contains(c.To))
                    throw new ProtocolException($"compute.over.to '{c.To}' unknown (field {f.Name})");
                if (c.Counts is not null)
                    foreach (var n in c.Counts)
                        if (!names.Contains(n)) throw new ProtocolException($"compute.counts '{n}' unknown (field {f.Name})");
            }
        }
    }

    // Hyphen-tolerant enum parse: JSON uses "length-prefix" but the enum is
    // FramingMode.LengthPrefix. Stripping hyphens is a no-op for the other
    // enums (FieldKind/CodecType/ByteOrder), which contain no hyphens.
    private static T ParseEnum<T>(string? s) where T : struct
        => Enum.Parse<T>(s!.Replace("-", ""), ignoreCase: true);

    // CS0649 (field never assigned) is expected here: these public fields are
    // populated by System.Text.Json reflection (IncludeFields=true), which the
    // compiler cannot see.
#pragma warning disable CS0649
    private static class Dto
    {
        public sealed class ProtocolDto { public string? Name; public string? Version; public string? DefaultByteOrder; public FramingDto? Framing; public FieldDto[]? Layout; public CommandDto[]? Commands; }
        public sealed class FramingDto { public string? Mode; public string[]? Preamble; public string? LengthField; public int FrameTimeoutMs; public string[]? Start; public string[]? End; }
        public sealed class FieldDto { public string? Name; public string? Kind; public string? Codec; public string? ByteOrder; public int? Size; public string[]? Value; public string? Default; public Dictionary<string,string>? Enum; public ComputeDto? Compute; }
        public sealed class ComputeDto { public string? Algo; public string[]? Counts; public int? Offset; public OverDto? Over; public Dictionary<string,JsonElement>? Params; }
        public sealed class OverDto { public string? From; public string? To; }
        public sealed class CommandDto { public string? Name; public string? Title; public Dictionary<string,string>? Fix; public PayloadFieldDto[]? PayloadFields; }
        public sealed class PayloadFieldDto { public string? Name; public string? Codec; public string? ByteOrder; public int? Size; public string? Default; }
    }
#pragma warning restore CS0649
}
