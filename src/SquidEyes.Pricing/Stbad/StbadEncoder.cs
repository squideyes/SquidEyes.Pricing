using System.IO.Compression;
using System.Text;

namespace SquidEyes.Pricing.Stbad;

/// <summary>
/// Encodes a <see cref="DepthTickSet"/> to the <c>.stbad</c> depth format: a fixed header, a
/// sequence of independently-decodable blocks (each = one keyframe + delta events, optionally
/// compressed), and a footer carrying the per-block seek index, total event count, and a CRC-32C
/// body checksum. Structural encoding (delta + zigzag + varint + changed-slot bitmap) does most of
/// the size work before the codec runs.
/// </summary>
public static class StbadEncoder
{
    public static void Encode(DepthTickSet tickSet, Stream output) =>
        Encode(tickSet, output, StbadOptions.Default);

    public static void Encode(DepthTickSet tickSet, Stream output, StbadOptions options)
    {
        ArgumentNullException.ThrowIfNull(tickSet);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);

        var hasCount = options.IncludeOrderCount;
        var work = new DepthBook((double)tickSet.Instrument.TickSize);

        using var body = new MemoryStream();
        var index = new List<(long FirstTsNs, long RelOffset, int EventCount)>();

        MemoryStream? payload = null;
        long blockStartTimeNs = 0, lastTimeNs = 0, lastTradePx = 0;
        var eventsInBlock = 0;

        // Reusable scratch for ordering multi-slot (QN) changes into deterministic bit order.
        var slotByBit = new SlotChange[2 * DepthBook.MaxLevels];
        var hasBit = new bool[2 * DepthBook.MaxLevels];

        void FlushBlock()
        {
            if (payload is null) return;
            var rel = body.Position;
            var compressed = Compress(payload.GetBuffer(), (int)payload.Length, options.Codec);
            WriteU32(body, (uint)compressed.Length);
            body.Write(compressed, 0, compressed.Length);
            index.Add((blockStartTimeNs, rel, eventsInBlock));
            payload.Dispose();
            payload = null;
        }

        var events = tickSet.Events;
        var changes = tickSet.Changes;

        foreach (var e in events)
        {
            var needNewBlock = payload is null
                || eventsInBlock >= options.KeyframeMaxEvents
                || (e.TimeNs - blockStartTimeNs) >= (long)options.KeyframeMaxMillis * 1_000_000L;

            if (needNewBlock)
            {
                FlushBlock();
                payload = new MemoryStream();
                WriteKeyframe(payload, work, e.TimeNs, hasCount);
                blockStartTimeNs = e.TimeNs;
                lastTimeNs = e.TimeNs;
                eventsInBlock = 0;
                lastTradePx = work.BidSize(0) > 0 ? work.BidPxTicks(0) : 0;
            }

            if (e.Type == DepthEventType.Trade)
                EncodeTrade(payload!, e, ref lastTimeNs, ref lastTradePx);
            else
                EncodeQuote(payload!, changes, e, work, hasCount, ref lastTimeNs, slotByBit, hasBit);

            eventsInBlock++;
        }

        FlushBlock();

        var bodyBytes = body.GetBuffer().AsSpan(0, (int)body.Length);
        var crc = Crc32C.Compute(bodyBytes);

        var head = BuildHeader(tickSet, options, index.Count, body.Length, out var headerLen);

        head.CopyTo(output);
        output.Write(bodyBytes);
        WriteFooter(output, index, headerLen, events.Count, crc);
    }

    private static MemoryStream BuildHeader(
        DepthTickSet ts, StbadOptions opt, int blockCount, long bodyLength, out long headerLen)
    {
        var head = new MemoryStream();
        using var w = new BinaryWriter(head, Encoding.ASCII, leaveOpen: true);

        w.Write(StbadFormat.Magic);                 // 6
        w.Write(StbadFormat.Version);               // 1
        w.Write(Fixed(ts.Instrument.Symbol.ToString(), 4)); // 4
        w.Write(Fixed(ts.Contract.Code, 4));        // 4
        w.Write(ts.Date.DayNumber);                 // 4
        w.Write((byte)ts.Source);                   // 1
        w.Write((byte)ts.Session);                  // 1
        w.Write(StbadFormat.Levels);                // 1
        w.Write((byte)(opt.IncludeOrderCount ? StbadFormat.FieldHasOrderCount : 0)); // 1
        w.Write(ts.Instrument.TickSize);            // 8 (double)
        w.Write(ts.Instrument.PointValue);          // 8 (double)
        w.Write(StbadFormat.TsResolutionNanos);     // 1
        w.Write(ts.SessionStart.Ticks);             // 8  (epoch anchor)
        w.Write(opt.Codec);                         // 1
        w.Write(opt.KeyframeMaxEvents);             // 4
        w.Write(opt.KeyframeMaxMillis);             // 4
        w.Write(blockCount);                        // 4
        w.Flush();

        headerLen = head.Length + 8;                // + footerOffset field below
        w.Write(headerLen + bodyLength);            // 8 (footerOffset, absolute)
        w.Flush();

        head.Position = 0;
        return head;
    }

    private static void WriteFooter(
        Stream output, List<(long FirstTsNs, long RelOffset, int EventCount)> index,
        long headerLen, long totalEvents, uint crc)
    {
        using var w = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        w.Write(index.Count);
        foreach (var (firstTsNs, relOffset, eventCount) in index)
        {
            w.Write(firstTsNs);
            w.Write(headerLen + relOffset); // absolute file offset of the block
            w.Write(eventCount);
        }
        w.Write(totalEvents);
        w.Write(crc);
    }

    private static void WriteKeyframe(Stream s, DepthBook book, long absTimeNs, bool hasCount)
    {
        s.WriteByte((byte)StbadFormat.TagKeyframe);
        StbadVarInt.WriteU(s, (ulong)absTimeNs);

        WriteLadder(s, book, BookSide.Bid, hasCount);
        WriteLadder(s, book, BookSide.Ask, hasCount);
    }

    private static void WriteLadder(Stream s, DepthBook book, BookSide side, bool hasCount)
    {
        var prev = 0;
        for (var level = 0; level < DepthBook.MaxLevels; level++)
        {
            int px, sz, ct;
            if (side == BookSide.Bid)
                (px, sz, ct) = (book.BidPxTicks(level), book.BidSize(level), book.BidCount(level));
            else
                (px, sz, ct) = (book.AskPxTicks(level), book.AskSize(level), book.AskCount(level));

            // Empty slots reuse prev (delta 0) so an empty tail costs ~1 byte/level, not a big jump.
            var cur = sz > 0 ? px : prev;
            StbadVarInt.WriteS(s, cur - prev);
            prev = cur;
            StbadVarInt.WriteU(s, (uint)sz);
            if (hasCount)
                StbadVarInt.WriteU(s, (uint)ct);
        }
    }

    private static void EncodeTrade(Stream s, in DepthTickSet.StoredEvent e,
        ref long lastTimeNs, ref long lastTradePx)
    {
        var tag = e.Aggressor == PriceKind.TradeBid ? StbadFormat.TagTradeHit : StbadFormat.TagTradeLift;
        s.WriteByte((byte)tag);
        StbadVarInt.WriteU(s, (ulong)(e.TimeNs - lastTimeNs));
        lastTimeNs = e.TimeNs;
        StbadVarInt.WriteS(s, e.TradePriceTicks - lastTradePx);
        lastTradePx = e.TradePriceTicks;
        StbadVarInt.WriteU(s, (uint)e.TradeSize);
    }

    private static void EncodeQuote(Stream s, IReadOnlyList<SlotChange> changes,
        in DepthTickSet.StoredEvent e, DepthBook work, bool hasCount,
        ref long lastTimeNs, SlotChange[] slotByBit, bool[] hasBit)
    {
        if (e.ChangeCount == 1)
        {
            var c = changes[e.ChangeOffset];
            var header = StbadFormat.TagQuote1
                | ((int)c.Side << StbadFormat.TagShift)
                | (c.Level << (StbadFormat.TagShift + 1));
            s.WriteByte((byte)header);
            StbadVarInt.WriteU(s, (ulong)(e.TimeNs - lastTimeNs));
            lastTimeNs = e.TimeNs;
            WriteSlot(s, c, work, hasCount);
            return;
        }

        // QN: build a 20-bit changed-slot bitmap (bids 0..9 then asks 10..19) and emit in bit order.
        var mask = 0;
        for (var i = 0; i < e.ChangeCount; i++)
        {
            var c = changes[e.ChangeOffset + i];
            var bit = (c.Side == BookSide.Bid ? 0 : DepthBook.MaxLevels) + c.Level;
            slotByBit[bit] = c;
            hasBit[bit] = true;
            mask |= 1 << bit;
        }

        s.WriteByte((byte)StbadFormat.TagQuoteN);
        StbadVarInt.WriteU(s, (ulong)(e.TimeNs - lastTimeNs));
        lastTimeNs = e.TimeNs;
        StbadVarInt.WriteU(s, (uint)mask);

        for (var bit = 0; bit < 2 * DepthBook.MaxLevels; bit++)
        {
            if (!hasBit[bit]) continue;
            WriteSlot(s, slotByBit[bit], work, hasCount);
            hasBit[bit] = false;
        }
    }

    private static void WriteSlot(Stream s, in SlotChange c, DepthBook work, bool hasCount)
    {
        int prevPx, prevCt;
        if (c.Side == BookSide.Bid)
            (prevPx, prevCt) = (work.BidPxTicks(c.Level), work.BidCount(c.Level));
        else
            (prevPx, prevCt) = (work.AskPxTicks(c.Level), work.AskCount(c.Level));

        StbadVarInt.WriteS(s, c.PriceTicks - prevPx);
        StbadVarInt.WriteU(s, (uint)c.Size);
        if (hasCount)
            StbadVarInt.WriteS(s, c.OrderCount - prevCt);

        work.Apply(in c); // update running book so subsequent deltas reference the new value
    }

    private static byte[] Compress(byte[] data, int length, byte codec)
    {
        if (codec == StbadFormat.CodecNone)
            return data.AsSpan(0, length).ToArray();

        if (codec != StbadFormat.CodecBrotli)
            throw new NotSupportedException($"Unsupported .stbad codec id {codec}.");

        using var ms = new MemoryStream();
        using (var brotli = new BrotliStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            brotli.Write(data, 0, length);
        return ms.ToArray();
    }

    private static void WriteU32(Stream s, uint value)
    {
        Span<byte> b = stackalloc byte[4];
        BitConverter.TryWriteBytes(b, value);
        s.Write(b);
    }

    private static byte[] Fixed(string value, int width)
    {
        var padded = value.Length >= width ? value[..width] : value.PadRight(width);
        return Encoding.ASCII.GetBytes(padded);
    }
}
