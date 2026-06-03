namespace SquidEyes.Pricing.Stbad;

/// <summary>
/// Shared on-the-wire constants for the <c>.stbad</c> depth format: magic, version, event tags,
/// codec ids, field flags, and the default keyframe cadence. Kept in one place so the encoder and
/// decoder can never drift.
/// </summary>
internal static class StbadFormat
{
    /// <summary>Header magic: "STBAD\0".</summary>
    public static readonly byte[] Magic = "STBAD\0"u8.ToArray();

    public const byte Version = 1;

    public const byte Levels = DepthBook.MaxLevels; // 10

    // ---- event tags (3-bit, in the low bits of the first byte of each record) ----
    public const int TagKeyframe = 0;
    public const int TagQuote1 = 1;   // single-slot quote update
    public const int TagQuoteN = 2;   // multi-slot quote update (re-rank)
    public const int TagTradeHit = 3; // trade, sell aggressor (TradeBid)
    public const int TagTradeLift = 4; // trade, buy aggressor (TradeAsk)

    public const int TagShift = 3;
    public const int TagMask = 0b111;

    // ---- fieldsPerLevel flags ----
    public const byte FieldHasOrderCount = 0b0000_0001;

    // ---- codec ids ----
    public const byte CodecNone = 0;
    public const byte CodecBrotli = 1;
    // 2 = LZ4, 3 = Zstd reserved for later.

    // ---- timestamp resolution ----
    public const byte TsResolutionNanos = 0;
    public const byte TsResolutionMicros = 1;

    // ---- default keyframe cadence (a new block/keyframe every N events or M ms, whichever first) ----
    public const int DefaultKeyframeMaxEvents = 4096;
    public const int DefaultKeyframeMaxMillis = 1500;

    /// <summary>Guard against absurd allocations when decoding an untrusted block.</summary>
    public const int MaxBlockPayloadBytes = 64 * 1024 * 1024;
}
