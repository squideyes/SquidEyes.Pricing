using System.Globalization;

namespace SquidEyes.Pricing.Stbad;

/// <summary>
/// Symmetric CSV companion to <see cref="StbadEncoder"/> — the depth analog of
/// <c>StbaCsvEncoder</c>. Emits one row per quote slot-change and one row per trade so a
/// <c>.stbad</c> stream diffs cleanly and pastes into a spreadsheet.
/// </summary>
/// <remarks>
/// <c>Kind</c> reuses the binary one-letter codes: <c>B</c> = bid slot, <c>A</c> = ask slot,
/// <c>H</c> = trade hit (TradeBid), <c>L</c> = trade lift (TradeAsk). For trades the
/// <c>Level</c>/<c>Count</c> columns are blank.
/// </remarks>
public static class StbadCsvEncoder
{
    public const string Header = "OnET,Kind,Level,Price,Size,Count";

    public static void Encode(DepthTickSet tickSet, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(tickSet);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine(Header);

        foreach (var (e, book) in tickSet.Replay())
        {
            var time = e.OnET.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture);

            if (e.Type == DepthEventType.Trade)
            {
                WriteRow(writer, time, e.Aggressor == PriceKind.TradeBid ? 'H' : 'L',
                    level: null, e.Price, e.Size, count: null);
                continue;
            }

            for (var i = 0; i < DepthBook.MaxLevels; i++)
                if ((e.BidChangedMask & (1 << i)) != 0)
                {
                    var lvl = book.Bid(i);
                    WriteRow(writer, time, 'B', i, lvl.Price, lvl.Size, lvl.OrderCount);
                }

            for (var i = 0; i < DepthBook.MaxLevels; i++)
                if ((e.AskChangedMask & (1 << i)) != 0)
                {
                    var lvl = book.Ask(i);
                    WriteRow(writer, time, 'A', i, lvl.Price, lvl.Size, lvl.OrderCount);
                }
        }
    }

    public static void Encode(DepthTickSet tickSet, Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var writer = new StreamWriter(output, new System.Text.UTF8Encoding(false), leaveOpen: true);
        Encode(tickSet, writer);
    }

    private static void WriteRow(
        TextWriter w, string time, char kind, int? level, double price, int size, int? count)
    {
        w.Write(time);
        w.Write(',');
        w.Write(kind);
        w.Write(',');
        if (level is { } l) w.Write(l.ToString(CultureInfo.InvariantCulture));
        w.Write(',');
        w.Write(price.ToString("0.#########", CultureInfo.InvariantCulture));
        w.Write(',');
        w.Write(size.ToString(CultureInfo.InvariantCulture));
        w.Write(',');
        if (count is { } c) w.Write(c.ToString(CultureInfo.InvariantCulture));
        w.WriteLine();
    }
}
