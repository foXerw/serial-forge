using System.Diagnostics;
using SerialForge.Core;
using SerialForge.Core.Models;

namespace SerialForge.Transport;

// Consumes arbitrary byte chunks (one byte at a time, big bursts, or anything
// in between), accumulates them, and raises FrameReady once per complete frame.
// Robust to weird chunking: it never inspects a frame until enough bytes are
// present, and it scans for the preamble to resync after garbage.
public sealed class Framer
{
    private readonly ProtocolDefinition _def;
    private readonly List<byte> _buf = new();
    private readonly byte[]? _preamble;
    private readonly Stopwatch _idle = new();

    // Length-prefix layout quantities, pre-computed once in the ctor.
    private readonly int _lenFieldOffset;   // bytes before the length field
    private readonly int _lenFieldSize;     // width of the length field
    private readonly ByteOrder _order;      // length field's byte order
    private readonly string? _payloadField; // the variable counted field (null if none)
    private readonly int _fixedBefore;      // fixed bytes before the payload
    private readonly int _fixedAfter;       // fixed bytes after the payload (e.g. crc)
    private readonly int _otherCounted;     // bytes the length field counts besides the payload
    private readonly int _offset;           // length field's compute offset

    public event EventHandler<byte[]>? FrameReady;

    public Framer(ProtocolDefinition def)
    {
        _def = def;
        _order = def.DefaultByteOrder;
        if (def.Framing.Preamble is { Length: > 0 })
            _preamble = def.Framing.Preamble.SelectMany(Parse).ToArray();

        // Locate the length field and pre-compute the layout-aware total-length
        // formula. The demo protocol's `len` field means "payload byte count"
        // (compute.counts=["payload"]), NOT "all bytes after the length field",
        // so the total is derived from the payload position rather than from the
        // length-field offset alone.
        var lengthField = Array.Find(def.Layout, f => f.Name == def.Framing.LengthField);
        if (lengthField is null) return;

        _lenFieldSize = FixedFieldSize(lengthField);
        _order = lengthField.ByteOrder ?? def.DefaultByteOrder;
        _lenFieldOffset = 0;
        foreach (var f in def.Layout)
        {
            if (f.Name == lengthField.Name) break;
            _lenFieldOffset += FixedFieldSize(f);
        }

        var counts = lengthField.Compute?.Counts ?? Array.Empty<string>();
        // The payload is the counted field with no fixed size.
        _payloadField = counts.FirstOrDefault(n => FixedFieldSize(FieldByName(def, n)) == 0);
        _offset = lengthField.Compute?.Offset ?? 0;

        if (_payloadField is not null)
        {
            _fixedBefore = SumFixedBefore(def, _payloadField);
            _fixedAfter = SumFixedAfter(def, _payloadField);
            _otherCounted = counts.Where(n => n != _payloadField)
                                  .Sum(n => FixedFieldSize(FieldByName(def, n)));
        }
        else
        {
            // No variable counted field: the frame is entirely fixed-size.
            _fixedBefore = def.Layout.Sum(FixedFieldSize);
            _fixedAfter = 0;
            _otherCounted = 0;
        }
    }

    public void Feed(byte[] chunk)
    {
        _buf.AddRange(chunk);
        _idle.Restart();
        Drain();
    }

    /// <summary>Call periodically (e.g. every 50ms) to flush partial frames on idle timeout.</summary>
    public void Tick()
    {
        if (_buf.Count > 0 && _idle.ElapsedMilliseconds >= _def.Framing.FrameTimeoutMs)
        {
            FrameReady?.Invoke(this, _buf.ToArray());
            _buf.Clear();
        }
    }

    private void Drain()
    {
        while (TryTakeOne(out var frame))
            FrameReady?.Invoke(this, frame!);
    }

    private bool TryTakeOne([System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out byte[] frame)
    {
        frame = null;
        if (_def.Framing.Mode == FramingMode.LengthPrefix)
        {
            int start = FindPreamble();
            if (start < 0) return false;
            if (start > 0) _buf.RemoveRange(0, start);
            int headerLen = _lenFieldOffset + _lenFieldSize;
            if (_buf.Count < headerLen) return false;
            long lenVal = ReadUInt(_buf, _lenFieldOffset, _lenFieldSize, _order);
            int total;
            if (_payloadField is not null)
            {
                int payloadSize = (int)(lenVal - _offset - _otherCounted);
                total = _fixedBefore + payloadSize + _fixedAfter;
            }
            else
            {
                // Fully fixed frame: the length field is not size-bearing.
                total = _fixedBefore;
            }
            if (_buf.Count < total) return false;
            frame = _buf.Take(total).ToArray();
            _buf.RemoveRange(0, total);
            return true;
        }
        if (_def.Framing.Mode == FramingMode.Delimiter && _def.Framing.End is { Length: > 0 } end)
        {
            var needle = end.SelectMany(Parse).ToArray();
            int idx = IndexOf(_buf, needle);
            if (idx < 0) return false;
            int total = idx + needle.Length;
            frame = _buf.Take(total).ToArray();
            _buf.RemoveRange(0, total);
            return true;
        }
        return false; // Timeout mode handled by Tick()
    }

    private int FindPreamble()
    {
        if (_preamble is null) return _buf.Count > 0 ? 0 : -1;
        return IndexOf(_buf, _preamble);
    }

    private static long ReadUInt(List<byte> buf, int off, int size, ByteOrder order)
    {
        long v = 0;
        for (int i = 0; i < size; i++)
        {
            int bi = order == ByteOrder.Little ? off + i : off + size - 1 - i;
            v |= ((long)buf[bi]) << (8 * i);
        }
        return v;
    }

    // Fixed byte size of a layout field, independent of any length field:
    //   Literal              -> its literal bytes length (preamble = 2)
    //   fixed-size codec     -> 1/2/4 (covers numeric Value like `cmd` AND
    //                          Computed like `len`, `crc16`)
    //   Size-declared field  -> f.Size (sized bytes/string)
    //   variable/payload     -> 0 (sized dynamically from the length field)
    private static int FixedFieldSize(FieldDef f)
    {
        if (f.Kind == FieldKind.Literal)
            return f.LiteralValue?.SelectMany(Parse).ToArray().Length ?? 0;
        int codecSize = f.Codec switch
        {
            CodecType.U8 => 1, CodecType.U16 => 2, CodecType.U32 => 4,
            _ => -1
        };
        if (codecSize > 0) return codecSize;
        if (f.Size is int s) return s;
        return 0;
    }

    private static FieldDef FieldByName(ProtocolDefinition def, string name) =>
        Array.Find(def.Layout, f => f.Name == name)
            ?? throw new InvalidOperationException($"layout field '{name}' not found");

    private static int SumFixedBefore(ProtocolDefinition def, string name)
    {
        int sum = 0;
        foreach (var f in def.Layout)
        {
            if (f.Name == name) break;
            sum += FixedFieldSize(f);
        }
        return sum;
    }

    private static int SumFixedAfter(ProtocolDefinition def, string name)
    {
        int sum = 0;
        bool past = false;
        foreach (var f in def.Layout)
        {
            if (f.Name == name) { past = true; continue; }
            if (past) sum += FixedFieldSize(f);
        }
        return sum;
    }

    private static int IndexOf(List<byte> hay, byte[] needle)
    {
        for (int i = 0; i <= hay.Count - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
                if (hay[i + j] != needle[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }

    private static byte[] Parse(string s) => new[] { Convert.ToByte(s.Replace("0x", ""), 16) };
}
