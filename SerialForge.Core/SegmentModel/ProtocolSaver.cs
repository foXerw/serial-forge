using System.Text.Json;
using SerialForge.Core;
using SerialForge.Core.Models;

namespace SerialForge.Core.SegmentModel;

// Inverse of ProtocolLoader: serializes a segment-model protocol to the same JSON
// schema, omitting defaults so files stay concise and round-trip cleanly.
public static class ProtocolSaver
{
    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public static string ToJson(ProtocolDefinition def)
    {
        var dto = new
        {
            name = def.Name,
            version = def.Version,
            defaultByteOrder = def.DefaultByteOrder == ByteOrder.Big ? "big" : "little",
            frame = def.Frame.Select(SegmentDto),
            commands = def.Commands.Select(CommandDto)
        };
        return JsonSerializer.Serialize(dto, Opt);
    }

    public static void ToFile(ProtocolDefinition def, string path) => File.WriteAllText(path, ToJson(def));

    private static object SegmentDto(Segment s)
    {
        var d = new Dictionary<string, object?>
        {
            ["name"] = s.Name,
            ["role"] = s.Role.ToString().ToLowerInvariant()
        };
        if (s.Width is int w) d["width"] = w;
        if (s.ByteOrder is { } bo) d["byteOrder"] = bo == ByteOrder.Big ? "big" : "little";
        if (s.Value is { } v) d["value"] = v;
        if (s.Default is { } def) d["default"] = def;
        if (s.Enum is { } e) d["enum"] = e;
        if (s.Counts is { } c) d["counts"] = c;
        if (s.Offset != 0) d["offset"] = s.Offset;
        if (s.Algo is { } a) d["algo"] = a;
        if (s.OverFrom is not null || s.OverTo is not null) d["over"] = new { from = s.OverFrom, to = s.OverTo };
        if (s.Params is { } p && p.Count > 0) d["params"] = CopyParams(p);
        return d;
    }

    private static object CommandDto(CommandDef c) => new
    {
        name = c.Name,
        title = c.Title,
        values = c.Values.Count == 0 ? null : c.Values
    };

    private static Dictionary<string, JsonElement> CopyParams(Dictionary<string, JsonElement> p)
        => p.ToDictionary(kv => kv.Key, kv => kv.Value);
}
