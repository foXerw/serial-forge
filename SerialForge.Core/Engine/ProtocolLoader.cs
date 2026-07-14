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

        // Guard each top-level section: previously the null-forgiving derefs below
        // threw a raw NullReferenceException when a section was missing. Surface a
        // clear ProtocolException naming the missing section instead.
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ProtocolException("missing 'name' in protocol");
        if (string.IsNullOrWhiteSpace(dto.Version)) throw new ProtocolException("missing 'version' in protocol");
        if (dto.Framing is null) throw new ProtocolException("missing 'framing' in protocol");
        if (dto.Layout is null || dto.Layout.Length == 0) throw new ProtocolException("missing 'layout' in protocol");
        if (dto.Commands is null) throw new ProtocolException("missing 'commands' in protocol");

        var order = dto.DefaultByteOrder == "big" ? ByteOrder.Big : ByteOrder.Little;
        var framing = new FramingRule(
            ParseEnum<FramingMode>(dto.Framing!.Mode),
            dto.Framing.Preamble,
            dto.Framing.LengthField,
            dto.Framing.FrameTimeoutMs,
            dto.Framing.Start,
            dto.Framing.End);

        var layout = dto.Layout!.Select(ToFieldDef).ToArray();
        var commands = dto.Commands!.Select(ToCommandDef).ToArray();

        var def = new ProtocolDefinition(dto.Name!, dto.Version!, order, framing, layout, commands);
        Validate(def);
        return def;
    }

    public static ProtocolDefinition LoadFile(string path) => Load(File.ReadAllText(path));

    private static FieldDef ToFieldDef(Dto.FieldDto f) => new(
        f.Name!, ParseEnum<FieldKind>(f.Kind!), ParseEnum<CodecType>(f.Codec!),
        f.ByteOrder == "big" ? ByteOrder.Big : f.ByteOrder == "little" ? ByteOrder.Little : null,
        f.Size, f.Value, f.Default, f.Enum, ToCompute(f.Compute), ToBits(f.Bits));

    private static ComputeSpec? ToCompute(Dto.ComputeDto? c) => c is null ? null : new(
        c.Algo!, c.Counts, c.Offset ?? 0, c.Over?.From, c.Over?.To, c.Params);

    private static BitFieldDef[]? ToBits(Dto.BitFieldDto[]? arr) => arr is null ? null
        : arr.Select(b => new BitFieldDef(b.Name!, b.Offset, b.Width, b.Enum, b.Default, b.IsLength)).ToArray();

    private static CommandDef ToCommandDef(Dto.CommandDto c) => new(
        c.Name!, c.Title ?? c.Name!, c.Fix ?? new(),
        (c.PayloadFields ?? Array.Empty<Dto.PayloadFieldDto>()).Select(p => new PayloadFieldDef(
            p.Name!, ParseEnum<CodecType>(p.Codec!),
            p.ByteOrder == "big" ? ByteOrder.Big : p.ByteOrder == "little" ? ByteOrder.Little : null,
            p.Size, p.Default, ToBits(p.Bits))).ToArray());

    public static void Validate(ProtocolDefinition def)
    {
        if (string.IsNullOrWhiteSpace(def.Name)) throw new ProtocolException("missing 'name' in protocol");
        if (string.IsNullOrWhiteSpace(def.Version)) throw new ProtocolException("missing 'version' in protocol");
        if (def.Layout is null || def.Layout.Length == 0) throw new ProtocolException("missing 'layout' in protocol");
        if (def.Commands is null) throw new ProtocolException("missing 'commands' in protocol");

        var names = def.Layout.Select(f => f.Name).ToHashSet();
        var framing = def.Framing;
        if (framing.Mode == FramingMode.LengthPrefix)
        {
            if (framing.LengthField is null || !names.Contains(framing.LengthField))
                throw new ProtocolException($"framing.lengthField '{framing.LengthField}' not in layout");
        }
        foreach (var f in def.Layout)
        {
            if (f.Bits is { } lbits)
            {
                if (f.Codec != CodecType.U8)
                    throw new ProtocolException($"bitfield '{f.Name}' must use codec u8");
                bool isLengthField = f.Kind == FieldKind.Computed && f.Compute?.Algo == "length";
                if (f.Kind == FieldKind.Value)
                {
                    if (lbits.Any(b => b.IsLength))
                        throw new ProtocolException($"bitfield '{f.Name}': isLength child only allowed on a length field");
                }
                else if (isLengthField)
                {
                    int n = lbits.Count(b => b.IsLength);
                    if (n != 1)
                        throw new ProtocolException($"bitfield '{f.Name}': length field needs exactly one isLength child (found {n})");
                }
                else
                {
                    throw new ProtocolException($"bitfield '{f.Name}' only allowed on value or length fields");
                }
                ValidateBits(f.Name, lbits);
            }
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
        foreach (var c in def.Commands)
            foreach (var pf in c.PayloadFields)
                if (pf.Bits is { } pbits)
                {
                    if (pf.Codec != CodecType.U8)
                        throw new ProtocolException($"bitfield '{c.Name}.{pf.Name}' must use codec u8");
                    if (pbits.Any(b => b.IsLength))
                        throw new ProtocolException($"bitfield '{c.Name}.{pf.Name}': isLength child only allowed on a length field");
                    ValidateBits($"{c.Name}.{pf.Name}", pbits);
                }
    }

    // Validate a bitfield group: each child's offset/width must fit in 8 bits,
    // names must be unique, defaults must fit the width, and ranges must not overlap.
    private static void ValidateBits(string owner, BitFieldDef[] bits)
    {
        var ranges = new List<(int Lo, int Hi)>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in bits)
        {
            if (b.Width < 1 || b.Offset < 0 || b.Offset + b.Width > 8)
                throw new ProtocolException($"bit '{owner}.{b.Name}': offset/width out of range (must stay within 8 bits)");
            if (!names.Add(b.Name))
                throw new ProtocolException($"bit '{owner}.{b.Name}': duplicate bit name");
            ranges.Add((b.Offset, b.Offset + b.Width));
            if (b.Default is { } d)
            {
                try
                {
                    ulong v = d.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? Convert.ToUInt64(d[2..], 16) : Convert.ToUInt64(d, 10);
                    if (v >> b.Width != 0)
                        throw new ProtocolException($"bit '{owner}.{b.Name}': default 0x{v:X} overflows {b.Width}-bit field");
                }
                catch (ProtocolException) { throw; }
                catch (Exception)
                {
                    throw new ProtocolException($"bit '{owner}.{b.Name}': default '{d}' is not parseable");
                }
            }
        }
        var sorted = ranges.OrderBy(x => x.Lo).ToList();
        for (int i = 1; i < sorted.Count; i++)
            if (sorted[i].Lo < sorted[i - 1].Hi)
                throw new ProtocolException($"bit '{owner}': overlapping bit ranges");
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
        public sealed class FieldDto { public string? Name; public string? Kind; public string? Codec; public string? ByteOrder; public int? Size; public string[]? Value; public string? Default; public Dictionary<string,string>? Enum; public ComputeDto? Compute; public BitFieldDto[]? Bits; }
        public sealed class ComputeDto { public string? Algo; public string[]? Counts; public int? Offset; public OverDto? Over; public Dictionary<string,JsonElement>? Params; }
        public sealed class OverDto { public string? From; public string? To; }
        public sealed class CommandDto { public string? Name; public string? Title; public Dictionary<string,string>? Fix; public PayloadFieldDto[]? PayloadFields; }
        public sealed class PayloadFieldDto { public string? Name; public string? Codec; public string? ByteOrder; public int? Size; public string? Default; public BitFieldDto[]? Bits; }
        public sealed class BitFieldDto { public string? Name; public int Offset; public int Width; public Dictionary<string,string>? Enum; public string? Default; public bool IsLength; }
    }
#pragma warning restore CS0649
}
