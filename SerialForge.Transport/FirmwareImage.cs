namespace SerialForge.Transport;

// A firmware file loaded into memory, sliced into fixed-size blocks padded with
// 0xFF (the engine requires the transfer 'data' payload sub-field to be a fixed
// size). totalCrc32 is a standard reflected CRC32 (poly 0xEDB88320) over the
// raw image bytes — independent of the frame-checksum algorithm.
public sealed class FirmwareImage
{
    public byte[] Bytes { get; }
    public int TotalSize { get; }
    public byte[] TotalCrc32 { get; }
    public int ChunkSize { get; }
    public int TotalBlocks => (TotalSize + ChunkSize - 1) / ChunkSize;

    private FirmwareImage(byte[] bytes, int chunkSize, byte[] crc)
    { Bytes = bytes; TotalSize = bytes.Length; ChunkSize = chunkSize; TotalCrc32 = crc; }

    public static FirmwareImage Load(string path, int chunkSize)
    {
        var bytes = File.ReadAllBytes(path);
        return new FirmwareImage(bytes, chunkSize, Crc32(bytes));
    }

    public IEnumerable<(int seq, int offset, byte[] block)> Chunks()
    {
        int seq = 0;
        for (int offset = 0; offset < TotalSize; offset += ChunkSize, seq++)
        {
            var block = new byte[ChunkSize];
            Array.Fill(block, (byte)0xFF);
            int n = Math.Min(ChunkSize, TotalSize - offset);
            Array.Copy(Bytes, offset, block, 0, n);
            yield return (seq, offset, block);
        }
    }

    // Standard CRC32 (PKZIP): reflected poly 0xEDB88320, init 0xFFFFFFFF, xorOut 0xFFFFFFFF.
    private static byte[] Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        crc ^= 0xFFFFFFFF;
        return BitConverter.GetBytes(crc);   // little-endian on x86/x64
    }
}
