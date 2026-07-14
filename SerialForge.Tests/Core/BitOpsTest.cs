using SerialForge.Core.Engine;

namespace SerialForge.Tests.Core;

public class BitOpsTest
{
    [Fact]
    public void Write_then_read_nibbles_msb_first()
    {
        var buf = new byte[1];
        BitOps.Write(buf, 0, 4, 0x1);   // high nibble
        BitOps.Write(buf, 4, 4, 0x3);   // low nibble
        Assert.Equal(0x13, buf[0]);
        Assert.Equal(0x1UL, BitOps.Read(buf, 0, 4));
        Assert.Equal(0x3UL, BitOps.Read(buf, 4, 4));
    }

    [Fact]
    public void Write_read_across_byte_boundary()
    {
        var buf = new byte[2];
        BitOps.Write(buf, 4, 12, 0x123);   // spans bit 4..15
        Assert.Equal(0x01, buf[0]);        // low nibble = high 4 bits of 0x123
        Assert.Equal(0x23, buf[1]);
        Assert.Equal(0x123UL, BitOps.Read(buf, 4, 12));
    }

    [Fact]
    public void Read_handles_unaligned_offset_within_byte()
    {
        // byte 0x53 -> high nibble 5, low nibble 3
        var buf = new byte[] { 0x53 };
        Assert.Equal(0x5UL, BitOps.Read(buf, 0, 4));
        Assert.Equal(0x3UL, BitOps.Read(buf, 4, 4));
    }
}
