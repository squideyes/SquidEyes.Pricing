using System.IO.Compression;

namespace SquidEyes.Pricing.Stbad;

/// <summary>
/// Low-level block decoding shared by the eager <see cref="StbadDecoder"/> and the streaming
/// <see cref="StbadReader"/>: block decompression, keyframe replay, and single delta-event decode.
/// Operates over a decompressed payload span with a manual cursor so the hot path allocates nothing
/// per event.
/// </summary>
internal static class StbadBlockCodec
{
    internal struct DecodedEvent
    {
        public DepthEventType Type;
        public long TimeNs;
        public PriceKind Aggressor;
        public int TradePriceTicks;
        public int TradeSize;
        public int BidMask;
        public int AskMask;
    }

    public static byte[] Decompress(byte[] compressed, int offset, int length, byte codec)
    {
        if (codec == StbadFormat.CodecNone)
            return compressed.AsSpan(offset, length).ToArray();

        if (codec != StbadFormat.CodecBrotli)
            throw new NotSupportedException($"Unsupported .stbad codec id {codec}.");

        using var src = new MemoryStream(compressed, offset, length, writable: false);
        using var brotli = new BrotliStream(src, CompressionMode.Decompress);
        using var dst = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int read, total = 0;
        while ((read = brotli.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > StbadFormat.MaxBlockPayloadBytes)
                throw new InvalidDataException(
                    $"Decompressed block exceeds {StbadFormat.MaxBlockPayloadBytes} bytes.");
            dst.Write(buffer, 0, read);
        }
        return dst.ToArray();
    }

    /// <summary>Reads the leading keyframe of a block into <paramref name="book"/>; returns its absolute ts (ns).</summary>
    public static long ReadKeyframe(ReadOnlySpan<byte> payload, ref int pos, DepthBook book, bool hasCount)
    {
        var tag = payload[pos++] & StbadFormat.TagMask;
        if (tag != StbadFormat.TagKeyframe)
            throw new InvalidDataException($"Expected keyframe tag at block start, got {tag}.");

        var absTs = (long)StbadVarInt.ReadU(payload, ref pos);
        ReadLadder(payload, ref pos, book, BookSide.Bid, hasCount);
        ReadLadder(payload, ref pos, book, BookSide.Ask, hasCount);
        return absTs;
    }

    private static void ReadLadder(
        ReadOnlySpan<byte> payload, ref int pos, DepthBook book, BookSide side, bool hasCount)
    {
        var prev = 0;
        for (var level = 0; level < DepthBook.MaxLevels; level++)
        {
            var cur = prev + (int)StbadVarInt.ReadS(payload, ref pos);
            prev = cur;
            var sz = (int)StbadVarInt.ReadU(payload, ref pos);
            var ct = hasCount ? (int)StbadVarInt.ReadU(payload, ref pos) : 0;
            if (side == BookSide.Bid)
                book.SetBid(level, cur, sz, ct);
            else
                book.SetAsk(level, cur, sz, ct);
        }
    }

    /// <summary>
    /// Decodes one delta event, mutating <paramref name="book"/> (quotes) and the trade-price anchor.
    /// When <paramref name="changes"/> is non-null, the quote's <see cref="SlotChange"/>s are appended
    /// (used by the eager decoder to rebuild a <see cref="DepthTickSet"/>).
    /// </summary>
    public static void ReadEvent(
        ReadOnlySpan<byte> payload, ref int pos, DepthBook book, bool hasCount,
        ref long lastTimeNs, ref long lastTradePx, ref DecodedEvent ev, List<SlotChange>? changes)
    {
        var header = payload[pos++];
        var tag = header & StbadFormat.TagMask;

        ev.BidMask = 0;
        ev.AskMask = 0;

        switch (tag)
        {
            case StbadFormat.TagTradeHit:
            case StbadFormat.TagTradeLift:
            {
                lastTimeNs += (long)StbadVarInt.ReadU(payload, ref pos);
                lastTradePx += StbadVarInt.ReadS(payload, ref pos);
                var size = (int)StbadVarInt.ReadU(payload, ref pos);
                ev.Type = DepthEventType.Trade;
                ev.TimeNs = lastTimeNs;
                ev.Aggressor = tag == StbadFormat.TagTradeHit ? PriceKind.TradeBid : PriceKind.TradeAsk;
                ev.TradePriceTicks = (int)lastTradePx;
                ev.TradeSize = size;
                return;
            }

            case StbadFormat.TagQuote1:
            {
                var side = (BookSide)((header >> StbadFormat.TagShift) & 0x1);
                var level = (header >> (StbadFormat.TagShift + 1)) & 0xF;
                lastTimeNs += (long)StbadVarInt.ReadU(payload, ref pos);
                ReadSlot(payload, ref pos, book, side, level, hasCount, ref ev, changes);
                ev.Type = DepthEventType.Quote;
                ev.TimeNs = lastTimeNs;
                return;
            }

            case StbadFormat.TagQuoteN:
            {
                lastTimeNs += (long)StbadVarInt.ReadU(payload, ref pos);
                var mask = (int)StbadVarInt.ReadU(payload, ref pos);
                for (var bit = 0; bit < 2 * DepthBook.MaxLevels; bit++)
                {
                    if ((mask & (1 << bit)) == 0) continue;
                    var side = bit < DepthBook.MaxLevels ? BookSide.Bid : BookSide.Ask;
                    var level = bit < DepthBook.MaxLevels ? bit : bit - DepthBook.MaxLevels;
                    ReadSlot(payload, ref pos, book, side, level, hasCount, ref ev, changes);
                }
                ev.Type = DepthEventType.Quote;
                ev.TimeNs = lastTimeNs;
                return;
            }

            default:
                throw new InvalidDataException($"Unexpected event tag {tag} inside block.");
        }
    }

    private static void ReadSlot(
        ReadOnlySpan<byte> payload, ref int pos, DepthBook book, BookSide side, int level,
        bool hasCount, ref DecodedEvent ev, List<SlotChange>? changes)
    {
        int prevPx, prevCt;
        if (side == BookSide.Bid)
            (prevPx, prevCt) = (book.BidPxTicks(level), book.BidCount(level));
        else
            (prevPx, prevCt) = (book.AskPxTicks(level), book.AskCount(level));

        var newPx = prevPx + (int)StbadVarInt.ReadS(payload, ref pos);
        var size = (int)StbadVarInt.ReadU(payload, ref pos);
        var count = hasCount ? prevCt + (int)StbadVarInt.ReadS(payload, ref pos) : 0;

        var change = new SlotChange(side, level, newPx, size, count);
        book.Apply(in change);
        changes?.Add(change);

        if (side == BookSide.Bid) ev.BidMask |= 1 << level;
        else ev.AskMask |= 1 << level;
    }
}
