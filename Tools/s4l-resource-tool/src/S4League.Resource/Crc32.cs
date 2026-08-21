namespace S4League.Resource;

/// <summary>
/// Standard CRC-32 (reflected, polynomial 0xEDB88320), matching BlubLib's implementation.
/// </summary>
internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var checksum = 0xFFFFFFFFu;
        foreach (var b in data)
            checksum = (checksum >> 8) ^ Table[(checksum & 0xFF) ^ b];
        return ~checksum;
    }
}
