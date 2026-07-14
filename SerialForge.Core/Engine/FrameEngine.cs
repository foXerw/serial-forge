using SerialForge.Core.Algorithms;
using SerialForge.Core.Codecs;
using SerialForge.Core.Exceptions;
using SerialForge.Core.Models;

namespace SerialForge.Core.Engine;

// Packs/parse a frame described as an ordered segment list. Offsets accumulate
// from each segment's bit width (MSB-first); a single variable payload segment
// is sized by the Length segment that counts it. No constraint solving: pack and
// parse are each a forward walk plus a second pass for Length/Checksum.
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

    public byte[] Pack(IReadOnlyDictionary<string, object> values)
    {
        // Resolve each segment's bit width and, where relevant, its content/value.
        var widths = new int[_frame.Length];
        var offsets = new int[_frame.Length];
        byte[]? payload = null;
        int total = 0;
        for (int i = 0; i < _frame.Length; i++)
        {
            var seg = _frame[i];
            int w = seg.Width ?? 0;
            if (seg.Role == SegmentRole.Value && seg.Width is null)
            {
                payload = ContentBytes(seg, values);
                w = payload.Length * 8;
            }
            widths[i] = w;
            offsets[i] = total;
            total += w;
        }
        if (total % 8 != 0)
            throw new ProtocolException($"frame is not byte-aligned: {total} bits");
        var buf = new byte[total / 8];

        // Pass 1: place Fixed + Value.
        for (int i = 0; i < _frame.Length; i++)
        {
            var seg = _frame[i];
            switch (seg.Role)
            {
                case SegmentRole.Fixed:
                    WriteBytes(buf, offsets[i], LiteralBytes(seg));
                    break;
                case SegmentRole.Value when seg.Width is null:
                    WriteBytes(buf, offsets[i], payload!);
                    break;
                case SegmentRole.Value:
                    WriteInt(buf, offsets[i], widths[i], ResolveInt(seg, values), seg.ByteOrder);
                    break;
            }
        }

        // Pass 2: compute Length + Checksum (ranges/counts now fully known).
        for (int i = 0; i < _frame.Length; i++)
        {
            var seg = _frame[i];
            if (seg.Role == SegmentRole.Length)
            {
                long countedBits = (seg.Counts ?? System.Array.Empty<string>()).Sum(n => BitsOf(n, widths, payload));
                long len = countedBits / 8 + seg.Offset;
                if ((ulong)len >> widths[i] != 0)
                    throw new ProtocolException($"length {len} overflows {widths[i]}-bit field '{seg.Name}'");
                WriteInt(buf, offsets[i], widths[i], (ulong)len, seg.ByteOrder);
            }
            else if (seg.Role == SegmentRole.Checksum)
            {
                int fromBit = offsets[Index(seg.OverFrom!)];
                int toSeg = Index(seg.OverTo!);
                int toBit = offsets[toSeg] + widths[toSeg];
                var range = new ArraySegment<byte>(buf, fromBit / 8, toBit / 8 - fromBit / 8).ToArray();
                var canonical = _algos.Get(seg.Algo!).Compute(range,
                    new ComputeSpec(seg.Algo!, null, 0, null, null, seg.Params));
                WriteOrdered(buf, offsets[i], widths[i], canonical, seg.ByteOrder);
            }
        }
        return buf;
    }

    private int BitsOf(string name, int[] widths, byte[]? payload)
    {
        int i = Index(name);
        return _frame[i].Width is null ? payload!.Length * 8 : widths[i];
    }

    private int Index(string name)
    {
        for (int i = 0; i < _frame.Length; i++)
            if (_frame[i].Name == name) return i;
        throw new ProtocolException($"unknown segment '{name}'");
    }

    // --- value resolution -------------------------------------------------

    private static byte[] ContentBytes(Segment seg, IReadOnlyDictionary<string, object> values)
    {
        if (values.TryGetValue(seg.Name, out var raw)) return ToBytes(raw);
        if (seg.Default is string d && d.Length > 0) return BytesCodec.ParseHex(d);
        return System.Array.Empty<byte>();
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
        => (seg.Value ?? System.Array.Empty<string>()).SelectMany(BytesCodec.ParseHex).ToArray();

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

    // --- parse ------------------------------------------------------------

    // Forward walk: size the variable payload from the Length segment that counts
    // it (read first — Length precedes payload), then decode every segment and
    // verify Length/Checksum. Never throws on bad device data.
    public DecodedFrame Parse(byte[] bytes)
    {
        try
        {
            var widths = new int[_frame.Length];
            int variable = -1;
            for (int i = 0; i < _frame.Length; i++)
            {
                if (_frame[i].Role == SegmentRole.Value && _frame[i].Width is null) { variable = i; widths[i] = 0; }
                else widths[i] = _frame[i].Width!.Value;
            }

            int payloadBytes = 0;
            if (variable >= 0)
                payloadBytes = ResolvePayloadBytes(bytes, widths, variable);
            if (variable >= 0) widths[variable] = payloadBytes * 8;

            var offsets = new int[_frame.Length];
            int total = 0;
            for (int i = 0; i < _frame.Length; i++) { offsets[i] = total; total += widths[i]; }
            if (total > bytes.Length * 8)
                return new DecodedFrame(Array.Empty<DecodedField>(), bytes,
                    $"truncated frame: need {total} bits, have {bytes.Length * 8}");

            var fields = new List<DecodedField>();
            string? frameError = null;
            for (int i = 0; i < _frame.Length; i++)
            {
                var seg = _frame[i];
                int off = offsets[i], w = widths[i];
                switch (seg.Role)
                {
                    case SegmentRole.Fixed:
                    {
                        var expect = LiteralBytes(seg);
                        var actual = ReadBytes(bytes, off, w / 8);
                        bool ok = actual.SequenceEqual(expect);
                        fields.Add(new DecodedField(seg.Name, Hex(actual), off / 8, w / 8, !ok));
                        break;
                    }
                    case SegmentRole.Value when seg.Width is null:
                    {
                        var content = ReadBytes(bytes, off, payloadBytes);
                        fields.Add(new DecodedField(seg.Name, Hex(content), off / 8, payloadBytes, false));
                        break;
                    }
                    case SegmentRole.Value:
                    {
                        ulong v = ReadInt(bytes, off, w, seg.ByteOrder);
                        object display = seg.Enum is not null && seg.Enum.TryGetValue(v.ToString(), out var es) ? es : v;
                        fields.Add(new DecodedField(seg.Name, display, off / 8, w / 8, false));
                        break;
                    }
                    case SegmentRole.Length:
                    {
                        ulong v = ReadInt(bytes, off, w, seg.ByteOrder);
                        long countedBits = (seg.Counts ?? Array.Empty<string>()).Sum(n => CountBits(n, widths, payloadBytes));
                        long expected = countedBits / 8 + seg.Offset;
                        bool ok = (long)v == expected;
                        fields.Add(new DecodedField(seg.Name, v, off / 8, w / 8, !ok));
                        if (!ok && frameError is null)
                            frameError = $"length mismatch on '{seg.Name}': got {v} expected {expected}";
                        break;
                    }
                    case SegmentRole.Checksum:
                    {
                        var actual = ReadBytes(bytes, off, w / 8);
                        int fromBit = offsets[Index(seg.OverFrom!)];
                        int toSeg = Index(seg.OverTo!);
                        int toBit = offsets[toSeg] + widths[toSeg];
                        var range = new ArraySegment<byte>(bytes, fromBit / 8, toBit / 8 - fromBit / 8).ToArray();
                        var canonical = _algos.Get(seg.Algo!).Compute(range,
                            new ComputeSpec(seg.Algo!, null, 0, null, null, seg.Params));
                        var expect = (seg.ByteOrder ?? _defaultOrder) == ByteOrder.Little ? canonical.Reverse().ToArray() : canonical;
                        bool ok = actual.SequenceEqual(expect);
                        fields.Add(new DecodedField(seg.Name, Hex(actual), off / 8, w / 8, !ok));
                        if (!ok && frameError is null)
                            frameError = $"checksum mismatch on '{seg.Name}': got {Hex(actual)} expected {Hex(expect)}";
                        break;
                    }
                }
            }
            return new DecodedFrame(fields.ToArray(), bytes, frameError);
        }
        catch (Exception ex)
        {
            return new DecodedFrame(Array.Empty<DecodedField>(), bytes, ex.Message);
        }
    }

    // Bytes of the variable payload: from the Length segment that counts it
    // (read at its offset — it precedes the payload), else the sink remainder.
    private int ResolvePayloadBytes(byte[] bytes, int[] widths, int variable)
    {
        var name = _frame[variable].Name;
        for (int i = 0; i < _frame.Length; i++)
        {
            var seg = _frame[i];
            if (seg.Role == SegmentRole.Length && seg.Counts is not null && seg.Counts.Contains(name))
            {
                int lenOff = 0;
                for (int j = 0; j < i; j++) lenOff += widths[j];
                ulong lenVal = ReadInt(bytes, lenOff, widths[i], seg.ByteOrder);
                long othersBits = seg.Counts.Where(n => n != name).Sum(n => CountBits(n, widths, 0));
                return (int)((long)lenVal - seg.Offset - othersBits / 8);
            }
        }
        // sink: remaining bytes not claimed by fixed segments
        int fixedBits = 0;
        for (int i = 0; i < _frame.Length; i++) if (i != variable) fixedBits += widths[i];
        return bytes.Length - fixedBits / 8;
    }

    private int CountBits(string name, int[] widths, int payloadBytes)
    {
        int i = Index(name);
        return _frame[i].Width is null ? payloadBytes * 8 : widths[i];
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
                // First frame byte is the high byte (big-endian) or low byte (little-endian).
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
