using SerialForge.Core.Algorithms;
using SerialForge.Core.Codecs;
using SerialForge.Core.Exceptions;
using SerialForge.Core.Models;

namespace SerialForge.Core.Engine;

public sealed class ProtocolEngine
{
    private readonly ProtocolDefinition _def;
    private readonly CodecRegistry _codecs = new();
    private readonly AlgorithmRegistry _algos = new();

    public ProtocolEngine(ProtocolDefinition def) => _def = def;

    public byte[] Encode(CommandInstance inst)
    {
        // 1. Resolve outer value fields: apply command.fix, then instance overrides.
        var values = ResolveOuterValues(inst);
        // 2. Pack payload bytes from payload sub-fields (or raw bytes input).
        byte[] payload = PackPayload(inst);
        values["payload"] = payload;

        // 3. Lay out all fields into segments, deferring computed fields.
        var segments = new List<(FieldDef field, byte[]? bytes)>();
        foreach (var f in _def.Layout)
            segments.Add((f, EncodeField(f, values)));

        // 4. Resolve length-computed fields (constraint solve from known sizes).
        ResolveLengths(segments);

        // 5. Resolve remaining computed fields (checksum/CRC over byte ranges).
        var buffer = AssembleWithHoles(segments, out var offsets);
        ResolveChecksums(segments, offsets, buffer);

        // 6. Re-assemble final (computed bytes now present) and return.
        return Assemble(segments);
    }

    private Dictionary<string, object> ResolveOuterValues(CommandInstance inst)
    {
        var values = new Dictionary<string, object>();
        foreach (var f in _def.Layout)
            if (f.Default is not null) values[f.Name] = f.Default;
        foreach (var (k, v) in inst.Command.Fix)
            values[k] = v;
        foreach (var (k, v) in inst.FieldValues)
            values[k] = v;
        return values;
    }

    private byte[] PackPayload(CommandInstance inst)
    {
        if (inst.Command.PayloadFields.Length == 0)
        {
            // raw bytes the user may have typed for the payload field, else empty
            if (inst.FieldValues.TryGetValue("payload", out var raw) && raw is string s && s.Length > 0)
                return BytesCodec.ParseHex(s);
            return Array.Empty<byte>();
        }

        using var ms = new MemoryStream();
        foreach (var pf in inst.Command.PayloadFields)
        {
            var codec = _codecs.Get(pf.Codec);
            int size = pf.Size ?? codec.FixedSize ?? throw new ProtocolException($"payload field '{pf.Name}' needs size");
            var order = pf.ByteOrder ?? _def.DefaultByteOrder;
            object val = inst.PayloadValues.TryGetValue(pf.Name, out var v) ? v : (object?)pf.Default ?? 0;
            ms.Write(codec.Encode(val, size, order));
        }
        return ms.ToArray();
    }

    private byte[]? EncodeField(FieldDef f, Dictionary<string, object> values)
    {
        if (f.Kind == FieldKind.Computed) return null; // resolved later
        var codec = f.Codec == CodecType.Enum ? new EnumCodec(CodecType.U8, f.EnumMap) : _codecs.Get(f.Codec);
        var order = f.ByteOrder ?? _def.DefaultByteOrder;
        return f.Kind switch
        {
            FieldKind.Literal => LiteralBytes(f),
            FieldKind.Value => EncodeValueField(f, codec, order, values),
            _ => null
        };
    }

    private static byte[] LiteralBytes(FieldDef f)
    {
        if (f.LiteralValue is null || f.LiteralValue.Length == 0)
            throw new ProtocolException($"literal field '{f.Name}' has no value");
        return f.LiteralValue.SelectMany(s => BytesCodec.ParseHex(s)).ToArray();
    }

    private byte[] EncodeValueField(FieldDef f, ICodec codec, ByteOrder order, Dictionary<string, object> values)
    {
        if (!values.TryGetValue(f.Name, out var val))
            throw new ProtocolException($"missing value for field '{f.Name}'");
        if (codec.FixedSize is int fixedSize)
            return codec.Encode(val, fixedSize, order);
        // variable bytes field: val is already a byte[] (payload) or hex string
        if (val is byte[] b) return b;
        return BytesCodec.ParseHex(val.ToString()!);
    }

    private void ResolveLengths(List<(FieldDef field, byte[]? bytes)> segments)
    {
        // segments holds value tuples: a foreach copy would discard writes, so
        // mutate by index and write the element back into the list.
        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            if (seg.field.Kind != FieldKind.Computed || seg.field.Compute?.Algo != "length") continue;
            var spec = seg.field;
            var counts = spec.Compute!.Counts ?? Array.Empty<string>();
            using var ms = new MemoryStream();
            foreach (var name in counts)
            {
                var target = segments.First(s => s.field.Name == name);
                if (target.bytes is null) throw new ProtocolException($"length counts unresolved field '{name}'");
                ms.Write(target.bytes);
            }
            var range = ms.ToArray();
            var computed = _algos.Get("length").Compute(range, spec.Compute!);
            seg.bytes = ApplyByteOrder(computed, spec.ByteOrder ?? _def.DefaultByteOrder, bigEndianCanonical: true);
            segments[i] = seg;
        }
    }

    private byte[] AssembleWithHoles(List<(FieldDef field, byte[]? bytes)> segments, out Dictionary<string, (int offset, int len)> offsets)
    {
        offsets = new();
        using var ms = new MemoryStream();
        foreach (var (field, bytes) in segments)
        {
            offsets[field.Name] = ((int)ms.Length, bytes?.Length ?? 0);
            if (bytes is not null) ms.Write(bytes);
            else
            {
                int reserve = FieldReserveSize(field);
                ms.Write(new byte[reserve]);
            }
        }
        return ms.ToArray();
    }

    private int FieldReserveSize(FieldDef f)
    {
        var codec = _codecs.Get(f.Codec == CodecType.Enum ? CodecType.U8 : f.Codec);
        return f.Size ?? codec.FixedSize ?? throw new ProtocolException($"cannot size computed field '{f.Name}'");
    }

    private void ResolveChecksums(List<(FieldDef field, byte[]? bytes)> segments, Dictionary<string, (int offset, int len)> offsets, byte[] buffer)
    {
        // segments holds value tuples: write back by index (see ResolveLengths).
        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            if (seg.field.Kind != FieldKind.Computed || seg.field.Compute?.Algo == "length") continue;
            var spec = seg.field;
            var (fromOff, _) = offsets[spec.Compute!.From!];
            var (_, toLen) = offsets[spec.Compute!.To!];
            int toEnd = offsets[spec.Compute!.To!].offset + toLen;
            int rangeLen = toEnd - fromOff;
            var range = new ArraySegment<byte>(buffer, fromOff, rangeLen).ToArray();
            var computed = _algos.Get(spec.Compute!.Algo).Compute(range, spec.Compute!);
            seg.bytes = ApplyByteOrder(computed, spec.ByteOrder ?? _def.DefaultByteOrder, bigEndianCanonical: true);
            segments[i] = seg;
        }
    }

    private static byte[] Assemble(List<(FieldDef field, byte[]? bytes)> segments)
    {
        using var ms = new MemoryStream();
        foreach (var (_, bytes) in segments)
        {
            if (bytes is null) throw new ProtocolException("unresolved computed field");
            ms.Write(bytes);
        }
        return ms.ToArray();
    }

    private static byte[] ApplyByteOrder(byte[] canonicalBigEndian, ByteOrder order, bool bigEndianCanonical)
    {
        if (order == ByteOrder.Big) return canonicalBigEndian;
        var copy = canonicalBigEndian.ToArray();
        Array.Reverse(copy);
        return copy;
    }

    // Decode implemented in Task 8.
    public DecodedFrame Decode(byte[] frame) => throw new NotImplementedException();
}
