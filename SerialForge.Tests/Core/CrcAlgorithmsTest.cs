using System.Text.Json;
using SerialForge.Core.Algorithms;
using SerialForge.Core.Models;

namespace SerialForge.Tests.Core;

public class CrcAlgorithmsTest
{
    private static ComputeSpec CcittFalse(string algo, int width) => new(algo, null, 0, null, null, new()
    {
        { "width", Json.Serialize(width) },
        { "poly", Json.Serialize("0x1021") },
        { "init", Json.Serialize("0xFFFF") },
        { "refIn", Json.Serialize(false) },
        { "refOut", Json.Serialize(false) },
        { "xorOut", Json.Serialize("0x0000") }
    });

    [Fact]
    public void Crc16_ccitt_false_ascii_123456789()
    {
        // Standard check value for CRC-16/CCITT-FALSE over "123456789" is 0x29B1.
        var data = System.Text.Encoding.ASCII.GetBytes("123456789");
        var outBytes = new Crc16Algorithm().Compute(data, CcittFalse("crc16", 16));
        Assert.Equal(new byte[] { 0x29, 0xB1 }, outBytes);
    }

    [Fact]
    public void Crc32_standard_check_value()
    {
        // CRC-32 (zlib) check value over "123456789" is 0xCBF43926.
        var data = System.Text.Encoding.ASCII.GetBytes("123456789");
        var spec = new ComputeSpec("crc32", null, 0, null, null, new()
        {
            { "poly", Json.Serialize("0x04C11DB7") },
            { "init", Json.Serialize("0xFFFFFFFF") },
            { "refIn", Json.Serialize(true) },
            { "refOut", Json.Serialize(true) },
            { "xorOut", Json.Serialize("0xFFFFFFFF") }
        });
        var outBytes = new Crc32Algorithm().Compute(data, spec);
        Assert.Equal(new byte[] { 0x26, 0x39, 0xF4, 0xCB }, outBytes);
    }

    private static class Json
    {
        public static JsonElement Serialize(bool b)
        {
            using var d = System.Text.Json.JsonDocument.Parse(b ? "true" : "false");
            return d.RootElement.Clone();
        }
        public static JsonElement Serialize(string s)
        {
            using var d = System.Text.Json.JsonDocument.Parse($"\"{s}\"");
            return d.RootElement.Clone();
        }
        public static JsonElement Serialize(int i)
        {
            using var d = System.Text.Json.JsonDocument.Parse(i.ToString());
            return d.RootElement.Clone();
        }
    }
}
