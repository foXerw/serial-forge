using System.Text.Json;

namespace SerialForge.Core.Algorithms;

public static class ParamExtensions
{
    public static ulong GetHex(this Dictionary<string, JsonElement>? p, string key, ulong fallback = 0)
    {
        if (p is null || !p.TryGetValue(key, out var el)) return fallback;
        if (el.ValueKind == JsonValueKind.String)
            return Convert.ToUInt64(el.GetString()!.Replace("0x", ""), 16);
        return el.GetUInt64();
    }

    public static bool GetBool(this Dictionary<string, JsonElement>? p, string key, bool fallback = false)
    {
        if (p is null || !p.TryGetValue(key, out var el)) return fallback;
        return el.ValueKind == JsonValueKind.True || (el.ValueKind == JsonValueKind.String && el.GetString() == "true");
    }
}
