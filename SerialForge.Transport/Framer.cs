using System.Diagnostics;
using SerialForge.Core;
using SerialForge.Core.Models;
using SerialForge.Core.SegmentModel;

namespace SerialForge.Transport;

// Consumes arbitrary byte chunks and raises FrameReady once per complete frame.
// Framing is derived from the segment template: the first Fixed segment is the
// preamble (resync marker) and the Length segment gives the frame's total size.
// Robust to weird chunking: never inspects a frame until enough bytes are present,
// and scans for the preamble to resync after garbage.
public sealed class Framer
{
    private readonly ProtocolDefinition _def;
    private readonly List<byte> _buf = new();
    private readonly byte[]? _preamble;
    private readonly Stopwatch _idle = new();

    private readonly bool _lengthPrefix;     // preamble + length segment present
    private readonly int _lenFieldOffset;    // bytes before the length field
    private readonly int _lenFieldSize;      // length field width in bytes
    private readonly ByteOrder _order;       // length field byte order
    private readonly string? _payloadField;  // the variable counted segment (null if none)
    private readonly int _fixedBefore;       // fixed bytes before the payload
    private readonly int _fixedAfter;        // fixed bytes after the payload (e.g. crc)
    private readonly int _otherCounted;      // bytes the length counts besides the payload
    private readonly int _offset;            // length field's compute offset

    public event EventHandler<byte[]>? FrameReady;

    public Framer(ProtocolDefinition def)
    {
        _def = def;
        var frame = def.Frame;

        var pre = Array.Find(frame, s => s.Role == SegmentRole.Fixed);
        if (pre is not null)
            _preamble = (pre.Value ?? Array.Empty<string>()).SelectMany(Parse).ToArray();

        var len = Array.Find(frame, s => s.Role == SegmentRole.Length);
        if (len is not null && _preamble is not null)
        {
            _lengthPrefix = true;
            _lenFieldSize = len.Width!.Value / 8;
            _order = len.ByteOrder ?? def.DefaultByteOrder;
            _lenFieldOffset = 0;
            foreach (var s in frame)
            {
                if (s.Name == len.Name) break;
                _lenFieldOffset += ByteWidth(s);
            }
            var counts = len.Counts ?? Array.Empty<string>();
            _payloadField = counts.FirstOrDefault(n => FieldByName(frame, n).Width is null);
            _offset = len.Offset;
            if (_payloadField is not null)
            {
                _fixedBefore = SumBefore(frame, _payloadField);
                _fixedAfter = SumAfter(frame, _payloadField);
                _otherCounted = counts.Where(n => n != _payloadField).Sum(n => ByteWidth(FieldByName(frame, n)));
            }
            else
            {
                _fixedBefore = frame.Sum(ByteWidth);
                _fixedAfter = 0;
                _otherCounted = 0;
            }
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
        if (_buf.Count > 0 && _idle.ElapsedMilliseconds >= _def.FrameTimeoutMs)
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
        if (_lengthPrefix)
        {
            int start = FindPreamble();
            if (start < 0) return false;
            if (start > 0) _buf.RemoveRange(0, start);
            int headerLen = _lenFieldOffset + _lenFieldSize;
            if (_buf.Count < headerLen) return false;
            long lenVal = ReadUInt(_buf, _lenFieldOffset, _lenFieldSize, _order);
            int total = _payloadField is not null
                ? _fixedBefore + (int)(lenVal - _offset - _otherCounted) + _fixedAfter
                : _fixedBefore;
            if (_buf.Count < total) return false;
            frame = _buf.Take(total).ToArray();
            _buf.RemoveRange(0, total);
            return true;
        }
        return false; // timeout-only framing handled by Tick()
    }

    private int FindPreamble()
        => _preamble is null ? (_buf.Count > 0 ? 0 : -1) : IndexOf(_buf, _preamble);

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

    // Byte width of a fixed-width segment (variable segments return 0 — they are
    // sized dynamically and excluded from fixed sums).
    private static int ByteWidth(Segment s) => s.Width is int w ? w / 8 : 0;

    private static Segment FieldByName(Segment[] frame, string name) =>
        Array.Find(frame, s => s.Name == name)
            ?? throw new InvalidOperationException($"segment '{name}' not found");

    private static int SumBefore(Segment[] frame, string name)
    {
        int sum = 0;
        foreach (var s in frame) { if (s.Name == name) break; sum += ByteWidth(s); }
        return sum;
    }

    private static int SumAfter(Segment[] frame, string name)
    {
        int sum = 0; bool past = false;
        foreach (var s in frame)
        {
            if (s.Name == name) { past = true; continue; }
            if (past) sum += ByteWidth(s);
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
