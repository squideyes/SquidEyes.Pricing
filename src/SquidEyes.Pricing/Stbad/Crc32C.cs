namespace SquidEyes.Pricing.Stbad;

/// <summary>
/// CRC-32C (Castagnoli, polynomial 0x1EDC6F41) over a byte buffer. Hand-rolled and table-based so
/// the library stays dependency-free (no <c>System.IO.Hashing</c> package). Used as the body
/// integrity checksum in the <c>.stbad</c> footer.
/// </summary>
internal static class Crc32C
{
    private const uint Polynomial = 0x82F63B78; // reflected 0x1EDC6F41
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
            crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (var i = 0u; i < 256; i++)
        {
            var crc = i;
            for (var j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ Polynomial : crc >> 1;
            table[i] = crc;
        }
        return table;
    }
}
