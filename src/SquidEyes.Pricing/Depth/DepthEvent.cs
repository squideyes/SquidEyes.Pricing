namespace SquidEyes.Pricing;

/// <summary>Whether a <see cref="DepthEvent"/> updates the book or prints a trade.</summary>
public enum DepthEventType : byte
{
    Quote = 1,
    Trade = 2
}

/// <summary>
/// One event in a <c>.stbad</c> stream, yielded alongside the materialized <see cref="DepthBook"/>.
/// </summary>
/// <remarks>
/// <para>For a <see cref="DepthEventType.Trade"/> event, <see cref="Aggressor"/> is
/// <see cref="PriceKind.TradeBid"/> (a "hit" — seller hit the bid) or
/// <see cref="PriceKind.TradeAsk"/> (a "lift" — buyer lifted the ask), and
/// <see cref="Price"/>/<see cref="Size"/> describe the print. Trades do not mutate the book.</para>
/// <para>For a <see cref="DepthEventType.Quote"/> event, the book has already been updated;
/// <see cref="BidChangedMask"/>/<see cref="AskChangedMask"/> are 10-bit masks (bit <c>i</c> set ⇒
/// level <c>i</c> changed) so absorption/iceberg-style consumers can see exactly which slots moved.
/// The trade fields are unset.</para>
/// </remarks>
public readonly record struct DepthEvent(
    DateTime OnET,
    DepthEventType Type,
    PriceKind Aggressor,
    double Price,
    int Size,
    int BidChangedMask,
    int AskChangedMask)
{
    /// <summary>Convenience factory for a trade event.</summary>
    public static DepthEvent Trade(DateTime onET, PriceKind aggressor, double price, int size) =>
        new(onET, DepthEventType.Trade, aggressor, price, size, 0, 0);

    /// <summary>Convenience factory for a quote (book-update) event.</summary>
    public static DepthEvent Quote(DateTime onET, int bidChangedMask, int askChangedMask) =>
        new(onET, DepthEventType.Quote, default, 0, 0, bidChangedMask, askChangedMask);
}
