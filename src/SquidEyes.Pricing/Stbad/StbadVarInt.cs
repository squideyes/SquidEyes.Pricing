namespace SquidEyes.Pricing.Stbad;

/// <summary>
/// LEB128 varint + zigzag helpers for the <c>.stbad</c> format. Uses 64-bit values because
/// timestamps are nanoseconds; signed values are zigzag-encoded first so small magnitudes stay short.
/// </summary>
internal static class StbadVarInt
{
    private const int MaxShift = 63;

    public static void WriteU(Stream s, ulong v)
    {
        while (v >= 0x80)
        {
            s.WriteByte((byte)(v | 0x80));
            v >>= 7;
        }
        s.WriteByte((byte)v);
    }

    public static void WriteS(Stream s, long v) => WriteU(s, ZigZag(v));

    public static ulong ReadU(Stream s)
    {
        ulong result = 0;
        var shift = 0;
        while (true)
        {
            var b = s.ReadByte();
            if (b < 0)
                throw new EndOfStreamException("Unexpected end of stream reading varint.");
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return result;
            shift += 7;
            if (shift > MaxShift)
                throw new InvalidDataException("VarInt too long (possible data corruption).");
        }
    }

    public static long ReadS(Stream s) => UnZigZag(ReadU(s));

    // ---- span readers (zero-alloc hot path) ----

    public static ulong ReadU(ReadOnlySpan<byte> buf, ref int pos)
    {
        ulong result = 0;
        var shift = 0;
        while (true)
        {
            if (pos >= buf.Length)
                throw new EndOfStreamException("Unexpected end of block reading varint.");
            var b = buf[pos++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return result;
            shift += 7;
            if (shift > MaxShift)
                throw new InvalidDataException("VarInt too long (possible data corruption).");
        }
    }

    public static long ReadS(ReadOnlySpan<byte> buf, ref int pos) => UnZigZag(ReadU(buf, ref pos));

    public static ulong ZigZag(long v) => (ulong)((v << 1) ^ (v >> 63));

    public static long UnZigZag(ulong z) => (long)(z >> 1) ^ -(long)(z & 1);
}
