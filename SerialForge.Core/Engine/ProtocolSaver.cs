using System.Text.Json;
using SerialForge.Core.Models;

namespace SerialForge.Core.Engine;

// Inverse of ProtocolLoader: serializes the immutable ProtocolDefinition record
// back to the same JSON schema demo-mcu.json uses, so files round-trip and stay
// human-editable. Hex emitted with lowercase 0x prefix (matches ParamExtensions.GetHex).
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
            framing = FramingDto(def.Framing),
            layout = def.Layout.Select(FieldDto),
            commands = def.Commands.Select(CommandDto)
        };
        return JsonSerializer.Serialize(dto, Opt);
    }

    public static void ToFile(ProtocolDefinition def, string path) => File.WriteAllText(path, ToJson(def));

    private static object FramingDto(FramingRule f) => new
    {
        mode = ModeStr(f.Mode),
        preamble = f.Preamble,
        lengthField = f.LengthField,
        frameTimeoutMs = f.FrameTimeoutMs,
        start = f.Start,
        end = f.End
    };

    private static object FieldDto(FieldDef f)
    {
        var dto = new Dictionary<string, object?>
        {
            ["name"] = f.Name,
            ["kind"] = f.Kind.ToString().ToLowerInvariant(),
            ["codec"] = CodecStr(f.Codec)
        };
        if (f.ByteOrder is { } bo) dto["byteOrder"] = bo == ByteOrder.Big ? "big" : "little";
        if (f.Size is { } sz) dto["size"] = sz;
        if (f.Kind == FieldKind.Literal) dto["value"] = f.LiteralValue;
        if (f.Kind == FieldKind.Value && f.Default is not null) dto["default"] = f.Default;
        if (f.EnumMap is not null) dto["enum"] = f.EnumMap;
        if (f.Compute is { } c) dto["compute"] = ComputeDto(c);
        return dto;
    }

    private static object ComputeDto(ComputeSpec c)
    {
        var dto = new Dictionary<string, object?> { ["algo"] = c.Algo };
        if (c.Counts is not null) dto["counts"] = c.Counts;
        if (c.Offset != 0) dto["offset"] = c.Offset;
        if (c.From is not null || c.To is not null) dto["over"] = new { from = c.From, to = c.To };
        if (c.Params is not null && c.Params.Count > 0) dto["params"] = FlattenParams(c.Params);
        return dto;
    }

    // Flatten the JsonElement params to plain CLR values so the hex strings
    // (poly/init/xorOut) survive as JSON strings with lowercase "0x" intact.
    // JsonSerializer.Serialize(JsonElement, typeof(string)) is rejected by
    // ValidateInputType, so read the value directly: string-kind -> GetString(),
    // everything else -> GetRawText() (the documented fallback).
    private static Dictionary<string, object?> FlattenParams(Dictionary<string, JsonElement> p)
        => p.ToDictionary(kv => kv.Key, kv => (object?)(kv.Value.ValueKind == JsonValueKind.String ? kv.Value.GetString() : kv.Value.GetRawText()));

    private static object CommandDto(CommandDef c) => new
    {
        name = c.Name,
        title = c.Title,
        fix = c.Fix.Count == 0 ? null : c.Fix,
        payloadFields = c.PayloadFields.Select(p => new
        {
            name = p.Name,
            codec = CodecStr(p.Codec),
            byteOrder = p.ByteOrder == null ? null : (p.ByteOrder == ByteOrder.Big ? "big" : "little"),
            size = p.Size,
            @default = p.Default
        })
    };

    private static string ModeStr(FramingMode m) => m switch
    {
        FramingMode.LengthPrefix => "length-prefix",
        FramingMode.Delimiter => "delimiter",
        FramingMode.Timeout => "timeout",
        _ => m.ToString().ToLowerInvariant()
    };
    private static string CodecStr(CodecType c) => c.ToString().ToLowerInvariant();
}
