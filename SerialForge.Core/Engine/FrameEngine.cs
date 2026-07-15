using SerialForge.Core.Algorithms;
using SerialForge.Core.Codecs;
using SerialForge.Core.Exceptions;
using SerialForge.Core.Models;

namespace SerialForge.Core.Engine;

// Packs/parses a frame described as an ordered segment list. Offsets accumulate
// from each segment's bit width (MSB-first); a single variable payload segment
// is sized by the Length segment that counts it. No constraint solving: pack and
// parse are each a forward walk plus a second pass for Length/Checksum.
//
// A command may carry a Payload sub-template (another Segment[]): its bytes are
// packed into / parsed out of the frame's variable payload segment, so different
// commands can structure their payloads differently using the same abstraction.
public sealed class FrameEngine
{
    private readonly Segment[] _frame;
    private readonly ByteOrder _defaultOrder;
    private readonly AlgorithmRegistry _algos = new();

    public FrameEngine(Segment[] frame, ByteOrder defaultOrder)
    {
        _frame = frame;
        _defaultOrder = defaultOrder;
    }

    public byte[] Pack(IReadOnlyDictionary<string, object> values, Segment[]? payloadTemplate = null)
    {
        byte[]? payloadContent = VariableContent(_frame, values, payloadTemplate);
        return PackInto(_frame, values, payloadContent);
    }

    public DecodedFrame Parse(byte[] bytes, Segment[]? payloadTemplate = null)
    {
        try { return ParseInto(_frame, bytes, payloadTemplate, ""); }
        catch (Exception ex) { return new DecodedFrame(Array.Empty<DecodedField>(), bytes, ex.Message); }
    }

    // Resolve the variable payload segment's content: from a command's payload
    // sub-template if present, else raw bytes supplied under the segment's name.
    private byte[]? VariableContent(Segment[] segs, IReadOnlyDictionary<string, object> values, Segment[]? payloadTemplate)
    {
        int vi = VariableIndex(segs);
        if (vi < 0) return null;
        if (payloadTemplate is { Length: > 0 }) return PackData(payloadTemplate, values);
        return ContentBytes(segs[vi], values);
    }

    // Pack a payload sub-template; its own variable segment (e.g. `data`) takes
    // raw bytes from `values`, so nesting bottoms out here.
    private byte[] PackData(Segment[] template, IReadOnlyDictionary<string, object> values)
    {
        byte[]? nested = VariableContent(template, values, null);
        return PackInto(template, values, nested);
    }

    // --- shared pack core -------------------------------------------------

    private byte[] PackInto(Segment[] segs, IReadOnlyDictionary<string, object> values, byte[]? variableContent)
    {
        int vi = VariableIndex(segs);
        var widths = new int[segs.Length];
        var offsets = new int[segs.Length];
        int total = 0;
        for (int i = 0; i < segs.Length; i++)
        {
            int w = segs[i].Width ?? 0;
            if (i == vi) w = (variableContent ?? Array.Empty<byte>()).Length * 8;
            widths[i] = w;
            offsets[i] = total;
            total += w;
        }
        if (total % 8 != 0)
            throw new ProtocolException($"frame is not byte-aligned: {total} bits");
        var buf = new byte[total / 8];

        for (int i = 0; i < segs.Length; i++)
        {
            var seg = segs[i];
            switch (seg.Role)
            {
                case SegmentRole.Fixed:
                    WriteBytes(buf, offsets[i], LiteralBytes(seg));
                    break;
                case SegmentRole.Value when i == vi:
                    WriteBytes(buf, offsets[i], variableContent!);
                    break;
                case SegmentRole.Value:
                {
                    ulong v = ResolveInt(seg, values);
                    if (widths[i] < 64 && v >> widths[i] != 0)
                        throw new ProtocolException($"value for '{seg.Name}' overflows {widths[i]} bits");
                    WriteInt(buf, offsets[i], widths[i], v, seg.ByteOrder);
                    break;
                }
            }
        }

        for (int i = 0; i < segs.Length; i++)
        {
            var seg = segs[i];
            if (seg.Role == SegmentRole.Length)
            {
                long countedBits = (seg.Counts ?? Array.Empty<string>()).Sum(n => BitsOf(segs, widths, n));
                long len = countedBits / 8 + seg.Offset;
                if ((ulong)len >> widths[i] != 0)
                    throw new ProtocolException($"length {len} overflows {widths[i]}-bit field '{seg.Name}'");
                WriteInt(buf, offsets[i], widths[i], (ulong)len, seg.ByteOrder);
            }
            else if (seg.Role == SegmentRole.Checksum)
            {
                int fromBit = offsets[Index(segs, seg.OverFrom!)];
                int toSeg = Index(segs, seg.OverTo!);
                int toBit = offsets[toSeg] + widths[toSeg];
                var range = new ArraySegment<byte>(buf, fromBit / 8, toBit / 8 - fromBit / 8).ToArray();
                var canonical = _algos.Get(seg.Algo!).Compute(range,
                    new ComputeSpec(seg.Algo!, null, 0, null, null, seg.Params));
                WriteOrdered(buf, offsets[i], widths[i], canonical, seg.ByteOrder);
            }
        }
        return buf;
    }

    private static int VariableIndex(Segment[] segs)
    {
        for (int i = 0; i < segs.Length; i++)
            if (segs[i].Role == SegmentRole.Value && segs[i].Width is null) return i;
        return -1;
    }

    private static int BitsOf(Segment[] segs, int[] widths, string name)
    {
        int i = Index(segs, name);
        return segs[i].Width is null ? widths[i] : widths[i];
    }

    private static int Index(Segment[] segs, string name)
    {
        for (int i = 0; i < segs.Length; i++)
            if (segs[i].Name == name) return i;
        throw new ProtocolException($"unknown segment '{name}'");
    }

    // --- value resolution -------------------------------------------------

    private static byte[] ContentBytes(Segment seg, IReadOnlyDictionary<string, object> values)
    {
        if (values.TryGetValue(seg.Name, out var raw)) return ToBytes(raw);
        if (seg.Default is string d && d.Length > 0) return BytesCodec.ParseHex(d);
        return Array.Empty<byte>();
    }

    private static ulong ResolveInt(Segment seg, IReadOnlyDictionary<string, object> values)
    {
        if (values.TryGetValue(seg.Name, out var raw)) return ParseInt(raw);
        if (seg.Default is string d && d.Length > 0) return ParseInt(d);
        return 0;
    }

    private static byte[] ToBytes(object raw) => raw switch
    {
        byte[] b => b,
        string s => BytesCodec.ParseHex(s),
        _ => BytesCodec.ParseHex(raw.ToString()!)
    };

    private static ulong ParseInt(object val) => val switch
    {
        ulong u => u,
        long l => (ulong)l,
        int n => (ulong)n,
        string s when s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) => Convert.ToUInt64(s[2..], 16),
        string s when string.IsNullOrWhiteSpace(s) => 0,
        string s => Convert.ToUInt64(s, 10),
        _ => Convert.ToUInt64(val)
    };

    private static byte[] LiteralBytes(Segment seg)
        => (seg.Value ?? Array.Empty<string>()).SelectMany(BytesCodec.ParseHex).ToArray();

    // --- bit writing ------------------------------------------------------

    private void WriteInt(byte[] buf, int bitOffset, int width, ulong value, ByteOrder? order)
    {
        if (width % 8 == 0)
        {
            var bytes = new byte[width / 8];
            for (int i = 0; i < bytes.Length; i++)
                bytes[bytes.Length - 1 - i] = (byte)(value >> (8 * i));
            WriteOrdered(buf, bitOffset, width, bytes, order);
        }
        else
        {
            BitOps.Write(buf, bitOffset, width, value);
        }
    }

    private void WriteOrdered(byte[] buf, int bitOffset, int width, byte[] canonicalBigEndian, ByteOrder? order)
    {
        var bytes = (order ?? _defaultOrder) == ByteOrder.Little ? canonicalBigEndian.Reverse().ToArray() : canonicalBigEndian;
        WriteBytes(buf, bitOffset, bytes);
    }

    private static void WriteBytes(byte[] buf, int bitOffset, byte[] bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
            BitOps.Write(buf, bitOffset + 8 * i, 8, bytes[i]);
    }

    // --- shared parse core ------------------------------------------------

    private DecodedFrame ParseInto(Segment[] segs, byte[] bytes, Segment[]? payloadTemplate, string prefix)
    {
        var widths = new int[segs.Length];
        int variable = VariableIndex(segs);
        for (int i = 0; i < segs.Length; i++)
            widths[i] = segs[i].Width ?? 0;

        int payloadBytes = 0;
        if (variable >= 0)
            payloadBytes = ResolvePayloadBytes(segs, widths, variable, bytes);
        if (variable >= 0) widths[variable] = payloadBytes * 8;

        var offsets = new int[segs.Length];
        int total = 0;
        for (int i = 0; i < segs.Length; i++) { offsets[i] = total; total += widths[i]; }

        var fields = new List<DecodedField>();
        string? frameError = null;
        if (segs.Length > 0)
        {
            int firstBit = offsets[0];
            int lastBit = offsets[^1] + widths[^1];
            if (payloadTemplate is null && total > bytes.Length * 8)
                return new DecodedFrame(Array.Empty<DecodedField>(), bytes,
                    $"truncated frame: need {total} bits, have {bytes.Length * 8}");
        }

        for (int i = 0; i < segs.Length; i++)
        {
            var seg = segs[i];
            int off = offsets[i], w = widths[i];
            string name = prefix.Length == 0 ? seg.Name : prefix + seg.Name;
            switch (seg.Role)
            {
                case SegmentRole.Fixed:
                {
                    var expect = LiteralBytes(seg);
                    var actual = ReadBytes(bytes, off, w / 8);
                    bool ok = actual.SequenceEqual(expect);
                    fields.Add(new DecodedField(name, Hex(actual), off / 8, w / 8, !ok));
                    break;
                }
                case SegmentRole.Value when i == variable:
                {
                    var content = ReadBytes(bytes, off, payloadBytes);
                    if (payloadTemplate is { Length: > 0 })
                    {
                        // Expand the payload via its sub-template; prefix keeps names unique.
                        var sub = ParseInto(payloadTemplate, content, null, name + ".");
                        fields.AddRange(sub.Fields);
                        if (sub.Error is { } e && frameError is null) frameError = e;
                    }
                    else
                    {
                        fields.Add(new DecodedField(name, content, off / 8, payloadBytes, false));
                    }
                    break;
                }
                case SegmentRole.Value:
                {
                    ulong v = ReadInt(bytes, off, w, seg.ByteOrder);
                    object display = seg.Enum is not null && seg.Enum.TryGetValue(v.ToString(), out var es) ? es : v;
                    fields.Add(new DecodedField(name, display, off / 8, w / 8, false));
                    break;
                }
                case SegmentRole.Length:
                {
                    ulong v = ReadInt(bytes, off, w, seg.ByteOrder);
                    long countedBits = (seg.Counts ?? Array.Empty<string>()).Sum(n => BitsOf(segs, widths, n));
                    long expected = countedBits / 8 + seg.Offset;
                    bool ok = (long)v == expected;
                    fields.Add(new DecodedField(name, v, off / 8, w / 8, !ok));
                    if (!ok && frameError is null)
                        frameError = $"length mismatch on '{name}': got {v} expected {expected}";
                    break;
                }
                case SegmentRole.Checksum:
                {
                    var actual = ReadBytes(bytes, off, w / 8);
                    int fromBit = offsets[Index(segs, seg.OverFrom!)];
                    int toSeg = Index(segs, seg.OverTo!);
                    int toBit = offsets[toSeg] + widths[toSeg];
                    var range = new ArraySegment<byte>(bytes, fromBit / 8, toBit / 8 - fromBit / 8).ToArray();
                    var canonical = _algos.Get(seg.Algo!).Compute(range,
                        new ComputeSpec(seg.Algo!, null, 0, null, null, seg.Params));
                    var expect = (seg.ByteOrder ?? _defaultOrder) == ByteOrder.Little ? canonical.Reverse().ToArray() : canonical;
                    bool ok = actual.SequenceEqual(expect);
                    fields.Add(new DecodedField(name, Hex(actual), off / 8, w / 8, !ok));
                    if (!ok && frameError is null)
                        frameError = $"checksum mismatch on '{name}': got {Hex(actual)} expected {Hex(expect)}";
                    break;
                }
            }
        }
        return new DecodedFrame(fields.ToArray(), bytes, frameError);
    }

    // Bytes of the variable payload: from the Length segment that counts it
    // (read first — Length precedes the payload), else the sink remainder.
    private int ResolvePayloadBytes(Segment[] segs, int[] widths, int variable, byte[] bytes)
    {
        var name = segs[variable].Name;
        for (int i = 0; i < segs.Length; i++)
        {
            var seg = segs[i];
            if (seg.Role == SegmentRole.Length && seg.Counts is not null && seg.Counts.Contains(name))
            {
                int lenOff = 0;
                for (int j = 0; j < i; j++) lenOff += widths[j];
                ulong lenVal = ReadInt(bytes, lenOff, widths[i], seg.ByteOrder);
                long othersBits = seg.Counts.Where(n => n != name).Sum(n => BitsOf(segs, widths, n));
                return (int)((long)lenVal - seg.Offset - othersBits / 8);
            }
        }
        int fixedBits = 0;
        for (int i = 0; i < segs.Length; i++) if (i != variable) fixedBits += widths[i];
        return bytes.Length - fixedBits / 8;
    }

    private static ulong ReadInt(byte[] buf, int bitOffset, int width, ByteOrder? order)
    {
        if (width % 8 == 0)
        {
            int n = width / 8;
            ulong v = 0;
            for (int i = 0; i < n; i++)
            {
                byte b = (byte)BitOps.Read(buf, bitOffset + 8 * i, 8);
                int pos = (order ?? ByteOrder.Big) == ByteOrder.Little ? i : n - 1 - i;
                v |= (ulong)b << (8 * pos);
            }
            return v;
        }
        return BitOps.Read(buf, bitOffset, width);
    }

    private static byte[] ReadBytes(byte[] buf, int bitOffset, int nBytes)
    {
        var arr = new byte[nBytes];
        for (int i = 0; i < nBytes; i++) arr[i] = (byte)BitOps.Read(buf, bitOffset + 8 * i, 8);
        return arr;
    }

    private static string Hex(byte[] b) => string.Join(' ', b.Select(x => x.ToString("X2")));
}
