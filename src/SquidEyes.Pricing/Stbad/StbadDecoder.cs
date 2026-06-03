using System.Runtime.InteropServices;
using System.Text;

namespace SquidEyes.Pricing.Stbad;

/// <summary>
/// Eager decoder: reads an entire <c>.stbad</c> file into a <see cref="DepthTickSet"/>, verifying the
/// body CRC-32C. For forward streaming or seek, use <see cref="StbadReader"/> instead.
/// </summary>
public static class StbadDecoder
{
    public static DepthTickSet Decode(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        using var r = new BinaryReader(input, Encoding.ASCII, leaveOpen: true);

        var header = StbadHeader.Read(r);

        var headerLen = input.Position;
        var bodyLen = (int)(header.FooterOffset - headerLen);
        var body = r.ReadBytes(bodyLen);
        if (body.Length != bodyLen)
            throw new InvalidDataException("Truncated .stbad body.");

        // Footer: skip the seek index, read totals + checksum.
        input.Position = header.FooterOffset;
        var blockCount = r.ReadInt32();
        input.Position += blockCount * (8L + 8L + 4L);
        var totalEvents = r.ReadInt64();
        var crc = r.ReadUInt32();

        if (Crc32C.Compute(body) != crc)
            throw new InvalidDataException("Corrupt .stbad: body checksum mismatch.");

        var builder = DepthTickSet.CreateBuilder(
            header.Instrument, header.Date, header.Contract, header.Session, header.Source);

        DecodeBody(body, header, builder);

        var tickSet = builder.Build();
        if (tickSet.Count != totalEvents)
            throw new InvalidDataException(
                $"Event count mismatch: footer says {totalEvents}, decoded {tickSet.Count}.");
        return tickSet;
    }

    private static void DecodeBody(byte[] body, StbadHeader header, DepthTickSet.Builder builder)
    {
        var sessionStart = header.SessionStart;
        var hasCount = header.HasOrderCount;
        var book = new DepthBook((double)header.Instrument.TickSize);
        var changes = new List<SlotChange>();
        var ev = default(StbadBlockCodec.DecodedEvent);

        var bodyPos = 0;
        while (bodyPos < body.Length)
        {
            var compLen = (int)BitConverter.ToUInt32(body, bodyPos);
            bodyPos += 4;
            var payload = StbadBlockCodec.Decompress(body, bodyPos, compLen, header.Codec);
            bodyPos += compLen;

            var pos = 0;
            book.ResetAll();
            var keyTs = StbadBlockCodec.ReadKeyframe(payload, ref pos, book, hasCount);
            var lastTimeNs = keyTs;
            long lastTradePx = book.BidSize(0) > 0 ? book.BidPxTicks(0) : 0;

            while (pos < payload.Length)
            {
                changes.Clear();
                StbadBlockCodec.ReadEvent(
                    payload, ref pos, book, hasCount, ref lastTimeNs, ref lastTradePx, ref ev, changes);

                var onET = sessionStart.AddTicks(ev.TimeNs / 100);
                if (ev.Type == DepthEventType.Trade)
                    builder.AddTrade(onET, ev.Aggressor, ev.TradePriceTicks, ev.TradeSize);
                else
                    builder.AddQuote(onET, CollectionsMarshal.AsSpan(changes));
            }
        }
    }
}
