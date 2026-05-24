using System.Globalization;

namespace SquidEyes.Pricing.Stba;

/// <summary>
/// Symmetric CSV companion to <see cref="StbaEncoder"/>. Writes the same logical content as
/// the binary STBA format — every <see cref="Tick"/> in the <see cref="TickSet"/>, in order —
/// but as <c>OnET,Kind,Price,Size</c> rows that diff cleanly, paste into a spreadsheet, and
/// stay readable in a code review.
/// </summary>
/// <remarks>
/// <para>The header row (<c>OnET,Kind,Price,Size</c>) is always written.</para>
/// <para><c>Kind</c> uses the same single-letter codes as the binary format:
/// <c>B</c> = Bid, <c>A</c> = Ask, <c>H</c> = TradeBid ("hit"), <c>L</c> = TradeAsk ("lift").
/// <c>Price</c> is rendered with trailing zeros stripped; timestamps are ISO-8601 ET with
/// millisecond precision.</para>
/// </remarks>
public static class StbaCsvEncoder
{
    /// <summary>CSV header row written before the first data row.</summary>
    public const string Header = "OnET,Kind,Price,Size";

    /// <summary>Writes <paramref name="tickSet"/> as CSV to <paramref name="writer"/>.</summary>
    public static void Encode(TickSet tickSet, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(tickSet);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine(Header);
        foreach (var tick in tickSet)
        {
            writer.Write(tick.OnET.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(KindCode(tick.Kind));
            writer.Write(',');
            writer.Write(tick.Price.ToString("0.#########", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.WriteLine(tick.Volume);
        }
    }

    /// <summary>Encodes to a UTF-8 <see cref="Stream"/> without a BOM.</summary>
    public static void Encode(TickSet tickSet, Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);

        using var writer = new StreamWriter(output, new System.Text.UTF8Encoding(false), leaveOpen: true);
        Encode(tickSet, writer);
    }

    /// <summary>
    /// One-letter wire code per <see cref="PriceKind"/>:
    /// <c>B</c>/<c>A</c>/<c>H</c>/<c>L</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Kind is not one of the four primitive members.</exception>
    public static char KindCode(PriceKind kind) => kind switch
    {
        PriceKind.Bid => 'B',
        PriceKind.Ask => 'A',
        PriceKind.TradeBid => 'H',
        PriceKind.TradeAsk => 'L',
        _ => throw new InvalidOperationException($"Unsupported PriceKind: {kind}"),
    };
}
