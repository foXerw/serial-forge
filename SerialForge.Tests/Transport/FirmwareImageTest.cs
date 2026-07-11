using SerialForge.Transport;

namespace SerialForge.Tests.Transport;

public class FirmwareImageTest
{
    private static string TempImage(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), "fw-" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void Chunks_pads_last_block_to_chunk_size()
    {
        var path = TempImage(new byte[100]);      // 100 bytes, chunkSize 64 -> 2 blocks
        var img = FirmwareImage.Load(path, 64);
        Assert.Equal(100, img.TotalSize);
        Assert.Equal(2, img.TotalBlocks);
        var blocks = img.Chunks().ToList();
        Assert.All(blocks, b => Assert.Equal(64, b.block.Length));   // all padded to 64
        Assert.Equal(0, blocks[0].seq);
        Assert.Equal(0, blocks[0].offset);
        Assert.Equal(1, blocks[1].seq);
        Assert.Equal(64, blocks[1].offset);
    }

    [Fact]
    public void TotalCrc32_is_four_bytes_and_stable()
    {
        var path = TempImage(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var img = FirmwareImage.Load(path, 64);
        Assert.Equal(4, img.TotalCrc32.Length);
        var again = FirmwareImage.Load(path, 64);
        Assert.Equal(img.TotalCrc32, again.TotalCrc32);
    }
}
