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

    public ProtocolDefinition Definition => _def;

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
        ResolveLengths(segments, values);

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
            if (pf.Bits is { } pbits)
            {
                ms.Write(PackBits(pf.Name, pbits, inst.PayloadValues));
                continue;
            }
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
        if (f.Kind == FieldKind.Value && f.Bits is { } lbits)
            return PackBits(f.Name, lbits, values);
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

    private void ResolveLengths(List<(FieldDef field, byte[]? bytes)> segments, Dictionary<string, object> values)
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

            if (spec.Bits is { } lbits)
            {
                // The length lives in one bit child; the rest of the byte holds other
                // attributes (version/flags). Compute the scalar length, fit it into
                // that child's width, then pack the whole byte from all children.
                var lenChild = lbits.First(b => b.IsLength);
                long lenVal = range.Length + spec.Compute!.Offset;
                if ((ulong)lenVal >> lenChild.Width != 0)
                    throw new ProtocolException($"length {lenVal} overflows {lenChild.Width}-bit field '{spec.Name}.{lenChild.Name}'");
                values[$"{spec.Name}.{lenChild.Name}"] = (ulong)lenVal;
                seg.bytes = PackBits(spec.Name, lbits, values);
            }
            else
            {
                var computed = _algos.Get("length").Compute(range, spec.Compute!);
                seg.bytes = ApplyByteOrder(computed, spec.ByteOrder ?? _def.DefaultByteOrder);
            }
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
            seg.bytes = ApplyByteOrder(computed, spec.ByteOrder ?? _def.DefaultByteOrder);
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

    // Pack bitfield children into one byte, MSB-first. Child values are read from
    // `values` under the compound key "{group}.{child}"; missing => child.Default => 0.
    private byte[] PackBits(string group, BitFieldDef[] bits, Dictionary<string, object> values)
    {
        byte b = 0;
        foreach (var child in bits)
        {
            object? raw = values.TryGetValue($"{group}.{child.Name}", out var v) ? v : (object?)child.Default;
            ulong n = ParseBitValue(raw);
            int width = child.Width;
            if (n >> width != 0)
                throw new ProtocolException($"bit '{group}.{child.Name}' value 0x{n:X} overflows {width}-bit field");
            int shift = 8 - child.Offset - width;
            int mask = (1 << width) - 1;
            b |= (byte)((n & (ulong)mask) << shift);
        }
        return new byte[] { b };
    }

    private static ulong ParseBitValue(object? val) => val switch
    {
        null => 0,
        ulong u => u,
        long l => (ulong)l,
        int i => (ulong)i,
        string s when s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) => Convert.ToUInt64(s[2..], 16),
        string s when string.IsNullOrWhiteSpace(s) => 0,
        string s => Convert.ToUInt64(s, 10),
        _ => Convert.ToUInt64(val)
    };

    private static byte[] ApplyByteOrder(byte[] canonicalBigEndian, ByteOrder order)
    {
        if (order == ByteOrder.Big) return canonicalBigEndian;
        var copy = canonicalBigEndian.ToArray();
        Array.Reverse(copy);
        return copy;
    }

    // Decode walks the layout to size each field, decodes values, and verifies
    // computed fields (length/checksum). It NEVER throws on bad device data:
    // truncated/exception paths set DecodedFrame.Error; a bad computed field sets
    // DecodedFrame.Error and marks that field IsError while still returning the
    // partially-decoded fields.
    public DecodedFrame Decode(byte[] frame)
    {
        try
        {
            var fields = new List<DecodedField>();
            int offset = 0;
            var resolvedSizes = new Dictionary<string, int>();
            // First pass: walk fixed/literal/length fields to learn each size.
            foreach (var f in _def.Layout)
            {
                int size = SizeFor(f, frame, offset, resolvedSizes);
                resolvedSizes[f.Name] = size;
                offset += size;
                // Detect truncation as soon as accumulated sizes outrun the frame
                // so a short buffer is reported cleanly rather than throwing later
                // inside SizeFor/ReadUInt when it dereferences beyond frame.Length.
                if (offset > frame.Length)
                    return new DecodedFrame(Array.Empty<DecodedField>(), frame,
                        $"truncated frame: need {offset} bytes, have {frame.Length}");
            }

            // Second pass: decode values + verify computed fields.
            offset = 0;
            foreach (var f in _def.Layout)
            {
                int size = resolvedSizes[f.Name];
                var order = f.ByteOrder ?? _def.DefaultByteOrder;

                if (f.Kind == FieldKind.Literal)
                {
                    fields.Add(new DecodedField(f.Name, ToHexDisplay(LiteralBytes(f)), offset, size, false));
                }
                else if (f.Kind == FieldKind.Computed && f.Bits is { } lbits)
                {
                    // Length shares its byte with other attributes: expand each child,
                    // and verify the isLength child against the recomputed count.
                    byte bv = frame[offset];
                    bool ok = true;
                    long actualLen = 0, expectedLen = 0;
                    foreach (var child in lbits)
                    {
                        int shift = 8 - child.Offset - child.Width;
                        int mask = (1 << child.Width) - 1;
                        ulong cv = (ulong)((bv >> shift) & mask);
                        if (child.IsLength)
                        {
                            var range = CountedRange(f.Compute!, resolvedSizes, frame);
                            expectedLen = range.Length + f.Compute!.Offset;
                            actualLen = (long)cv;
                            ok = expectedLen == actualLen;
                        }
                        object display = child.Enum is not null && child.Enum.TryGetValue(cv.ToString(), out var es) ? es : cv;
                        fields.Add(new DecodedField($"{f.Name}.{child.Name}", display, offset, size, child.IsLength && !ok));
                    }
                    if (!ok)
                        return new DecodedFrame(fields.ToArray(), frame,
                            $"length mismatch on '{f.Name}': got {actualLen} expected {expectedLen}");
                }
                else if (f.Kind == FieldKind.Computed)
                {
                    var (ok, actual, expected) = VerifyComputed(f, frame, offset, size, resolvedSizes);
                    fields.Add(new DecodedField(f.Name, ToHexDisplay(actual), offset, size, !ok));
                    if (!ok)
                        return new DecodedFrame(fields.ToArray(), frame,
                            $"checksum mismatch on '{f.Name}': got {ToHexDisplay(actual)} expected {ToHexDisplay(expected)}");
                }
                else if (f.Kind == FieldKind.Value && f.Bits is { } dbits)
                {
                    byte bv = frame[offset];
                    foreach (var child in dbits)
                    {
                        int shift = 8 - child.Offset - child.Width;
                        int mask = (1 << child.Width) - 1;
                        ulong cv = (ulong)((bv >> shift) & mask);
                        object display = child.Enum is not null && child.Enum.TryGetValue(cv.ToString(), out var es) ? es : cv;
                        fields.Add(new DecodedField($"{f.Name}.{child.Name}", display, offset, size, false));
                    }
                }
                else // Value
                {
                    var codec = f.Codec == CodecType.Enum ? new EnumCodec(CodecType.U8, f.EnumMap) : _codecs.Get(f.Codec);
                    var (val, _) = codec.Decode(frame, offset, size, order);
                    fields.Add(new DecodedField(f.Name, val, offset, size, false));
                }
                offset += size;
            }
            return new DecodedFrame(fields.ToArray(), frame, null);
        }
        catch (Exception ex)
        {
            // Decode must never throw into the reader thread.
            return new DecodedFrame(Array.Empty<DecodedField>(), frame, ex.Message);
        }
    }

    // Resolve the byte size of a single field. Priority:
    //   1. Literal              -> its literal bytes length
    //   2. fixed-size codec     -> codec.FixedSize  (covers Computed fields like
    //                              len/crc16 AND numeric Value fields like cmd)
    //   3. Size-declared field  -> f.Size (bytes/string with explicit size)
    //   4. variable Value field -> derived from the length field that counts it
    //   5. sink                 -> remaining bytes in the frame
    // Branch 2 is load-bearing: without it, Computed fields (len, crc16 are both
    // u16) fall through to the sink path and get mis-sized, breaking the round trip.
    private int SizeFor(FieldDef f, byte[] frame, int offset, Dictionary<string, int> resolved)
    {
        if (f.Kind == FieldKind.Literal) return LiteralBytes(f).Length;
        var codec = _codecs.Get(f.Codec == CodecType.Enum ? CodecType.U8 : f.Codec);
        if (codec.FixedSize is int fs) return fs;   // Computed + numeric Value
        if (f.Size is int s) return s;              // Size-declared bytes/string

        // Variable bytes/payload: derive from the length field that counts it.
        var lengthField = _def.Layout.FirstOrDefault(o => o.Kind == FieldKind.Computed && o.Compute?.Algo == "length"
            && o.Compute.Counts is not null && o.Compute.Counts.Contains(f.Name));
        if (lengthField is not null)
        {
            int lenFieldOff = SumSizesBefore(lengthField.Name, resolved);
            long lenVal;
            if (lengthField.Bits is { } lenbits)
            {
                // Length shares its byte with other attributes: only the isLength
                // child's bits hold the count, not the whole byte.
                var lenChild = lenbits.First(b => b.IsLength);
                byte bv = frame[lenFieldOff];
                int shift = 8 - lenChild.Offset - lenChild.Width;
                int mask = (1 << lenChild.Width) - 1;
                lenVal = (bv >> shift) & mask;
            }
            else
            {
                int lenSize = resolved[lengthField.Name];
                lenVal = ReadUInt(frame, lenFieldOff, lenSize, lengthField.ByteOrder ?? _def.DefaultByteOrder);
            }
            long sumOthers = (lengthField.Compute!.Counts ?? Array.Empty<string>())
                .Where(n => n != f.Name).Sum(n => resolved[n]);
            return (int)(lenVal - sumOthers - lengthField.Compute.Offset);
        }
        // sink: remaining bytes
        int consumed = resolved.Values.Sum();
        return frame.Length - consumed;
    }

    private static long ReadUInt(byte[] frame, int off, int size, ByteOrder order)
    {
        long v = 0;
        for (int i = 0; i < size; i++)
        {
            int bi = order == ByteOrder.Little ? off + i : off + size - 1 - i;
            v |= ((long)frame[bi]) << (8 * i);
        }
        return v;
    }

    private (bool ok, byte[] actual, byte[] expected) VerifyComputed(
        FieldDef f, byte[] frame, int off, int size, Dictionary<string, int> resolved)
    {
        var spec = f.Compute!;
        if (spec.Algo == "length")
        {
            var expected = _algos.Get("length").Compute(CountedRange(spec, resolved, frame), spec);
            var actual = new ArraySegment<byte>(frame, off, size).ToArray();
            return (actual.SequenceEqual(Ordered(expected, f)), actual, Ordered(expected, f));
        }
        // checksum range from..to
        int fromOff = SumSizesBefore(spec.From!, resolved);
        int toEnd = SumSizesBefore(spec.To!, resolved) + resolved[spec.To!];
        var range = new ArraySegment<byte>(frame, fromOff, toEnd - fromOff).ToArray();
        var expectedCk = _algos.Get(spec.Algo).Compute(range, spec);
        var actualCk = new ArraySegment<byte>(frame, off, size).ToArray();
        var ordered = Ordered(expectedCk, f);
        return (actualCk.SequenceEqual(ordered), actualCk, ordered);
    }

    private byte[] CountedRange(ComputeSpec spec, Dictionary<string, int> resolved, byte[] frame)
    {
        using var ms = new MemoryStream();
        foreach (var n in spec.Counts ?? Array.Empty<string>())
            ms.Write(new ArraySegment<byte>(frame, SumSizesBefore(n, resolved), resolved[n]));
        return ms.ToArray();
    }

    // Sum of resolved sizes for all layout fields preceding `name`.
    private int SumSizesBefore(string name, Dictionary<string, int> resolved)
    {
        int sum = 0;
        foreach (var f in _def.Layout)
        {
            if (f.Name == name) break;
            sum += resolved[f.Name];
        }
        return sum;
    }

    // Apply the field's byte order to the algorithm's big-endian canonical bytes.
    private byte[] Ordered(byte[] canonical, FieldDef f)
    {
        var order = f.ByteOrder ?? _def.DefaultByteOrder;
        if (order == ByteOrder.Big) return canonical;
        var c = canonical.ToArray();
        Array.Reverse(c);
        return c;
    }

    private static string ToHexDisplay(byte[] b) =>
        string.Join(' ', b.Select(x => x.ToString("X2")));
}
