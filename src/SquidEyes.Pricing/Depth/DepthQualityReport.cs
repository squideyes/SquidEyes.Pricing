using System.Globalization;
using System.Text;
using SquidEyes.Pricing.Stbad;

namespace SquidEyes.Pricing;

/// <summary>
/// Audit summary for a <c>.stbad</c> file — the depth analog of <c>TickSetQuality</c>. Reports
/// per-level fill rates, crossed/locked frequency, the largest inter-event gap, the keyframe/delta
/// (snapshot/delta) ratio, and bytes-per-event, so a day's depth capture can be sanity-checked the
/// way <c>tickr validate</c> audits <c>.stba</c>.
/// </summary>
public sealed record DepthQualityReport
{
    public required Instrument Instrument { get; init; }
    public required Contract Contract { get; init; }
    public required DateOnly Date { get; init; }
    public required SessionKind Session { get; init; }

    public required long TotalEvents { get; init; }
    public required long QuoteEvents { get; init; }
    public required long TradeEvents { get; init; }
    public required int Keyframes { get; init; }

    /// <summary>Fraction of events for which bid level <c>i</c> was occupied (index 0..9).</summary>
    public required double[] BidFillRate { get; init; }
    public required double[] AskFillRate { get; init; }

    public required double CrossedFraction { get; init; }
    public required double LockedFraction { get; init; }
    public required TimeSpan MaxGap { get; init; }

    public required long FileBytes { get; init; }
    public double BytesPerEvent => TotalEvents == 0 ? 0 : (double)FileBytes / TotalEvents;

    /// <summary>Keyframes per delta event — the snapshot/delta ratio.</summary>
    public double SnapshotDeltaRatio => TotalEvents == 0 ? 0 : (double)Keyframes / TotalEvents;

    public static DepthQualityReport Analyze(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var fileBytes = input.Length;

        // Header + footer for structural counts (keyframes == block count, total events).
        input.Position = 0;
        StbadHeader header;
        int blockCount;
        long totalFromFooter;
        using (var r = new BinaryReader(input, Encoding.ASCII, leaveOpen: true))
        {
            header = StbadHeader.Read(r);
            input.Position = header.FooterOffset;
            blockCount = r.ReadInt32();
            input.Position += blockCount * (8L + 8L + 4L);
            totalFromFooter = r.ReadInt64();
        }

        long quotes = 0, trades = 0, sampled = 0, crossed = 0, locked = 0;
        var bidFill = new long[DepthBook.MaxLevels];
        var askFill = new long[DepthBook.MaxLevels];
        var maxGapNs = 0L;
        var lastNs = long.MinValue;

        input.Position = 0;
        using (var reader = new StbadReader(input))
        {
            while (reader.Read(out var e))
            {
                var ns = (e.OnET - header.SessionStart).Ticks * 100L;
                if (lastNs != long.MinValue)
                    maxGapNs = Math.Max(maxGapNs, ns - lastNs);
                lastNs = ns;

                if (e.Type == DepthEventType.Trade) { trades++; continue; }

                quotes++;
                sampled++;
                var book = reader.Book;
                for (var i = 0; i < DepthBook.MaxLevels; i++)
                {
                    if (book.Bid(i).Size > 0) bidFill[i]++;
                    if (book.Ask(i).Size > 0) askFill[i]++;
                }
                if (book.IsCrossed) crossed++;
                if (book.IsLocked) locked++;
            }
        }

        var total = quotes + trades;
        var denom = Math.Max(1, sampled);

        return new DepthQualityReport
        {
            Instrument = header.Instrument,
            Contract = header.Contract,
            Date = header.Date,
            Session = header.Session,
            TotalEvents = total,
            QuoteEvents = quotes,
            TradeEvents = trades,
            Keyframes = blockCount,
            BidFillRate = Array.ConvertAll(bidFill, c => (double)c / denom),
            AskFillRate = Array.ConvertAll(askFill, c => (double)c / denom),
            CrossedFraction = (double)crossed / denom,
            LockedFraction = (double)locked / denom,
            MaxGap = TimeSpan.FromTicks(maxGapNs / 100),
            FileBytes = fileBytes,
        };
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{Instrument.Symbol} {Contract.Code} {Date:yyyy-MM-dd} {Session.ToCode()}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  events={TotalEvents:N0} (quotes={QuoteEvents:N0}, trades={TradeEvents:N0})  keyframes={Keyframes:N0}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  bytes/event={BytesPerEvent:F2}  snapshot/delta={SnapshotDeltaRatio:P3}  maxGap={MaxGap}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  crossed={CrossedFraction:P3}  locked={LockedFraction:P3}");
        sb.Append("  bidFill=[");
        sb.AppendJoin(' ', Array.ConvertAll(BidFillRate, f => f.ToString("P0", CultureInfo.InvariantCulture)));
        sb.AppendLine("]");
        sb.Append("  askFill=[");
        sb.AppendJoin(' ', Array.ConvertAll(AskFillRate, f => f.ToString("P0", CultureInfo.InvariantCulture)));
        sb.Append(']');
        return sb.ToString();
    }
}
