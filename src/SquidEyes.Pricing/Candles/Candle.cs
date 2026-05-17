namespace SquidEyes.Pricing;

/// <summary>
/// A single OHLCV bar over the half-open ET window <c>[FromET, UntilET)</c>. Trade-only:
/// only ticks whose <see cref="PriceKind"/> matches <see cref="PriceKind.Trade"/>
/// (i.e. <see cref="PriceKind.TradeBid"/> or <see cref="PriceKind.TradeAsk"/>) contribute.
///
/// The candle is intentionally interval-agnostic — the window is whatever the caller
/// chooses (1-minute, 5-minute, session-VWAP-bucket, anchored-volume, etc.).
///
/// Two construction modes:
///   * Load a pre-computed candle via the OHLCV constructor.
///   * Start with an empty window via the two-arg constructor, then call
///     <see cref="AddTrade(double, int)"/> or <see cref="TryAdd"/> as ticks arrive.
///
/// Mutable by design — building a candle one trade at a time would otherwise allocate
/// a new instance per tick. Not thread-safe.
/// </summary>
public sealed class Candle
{
    public DateTime FromET  { get; }
    public DateTime UntilET { get; }

    public double Open  { get; private set; }
    public double High  { get; private set; }
    public double Low   { get; private set; }
    public double Close { get; private set; }
    public int    Volume    { get; private set; }
    public int    TickCount { get; private set; }

    /// <summary>True if no trades have been recorded yet.</summary>
    public bool IsEmpty => TickCount == 0;

    /// <summary>
    /// Constructs an empty candle covering the half-open ET window <c>[fromEt, untilEt)</c>.
    /// O/H/L/C are zero until the first <see cref="AddTrade(double, int)"/> call.
    /// </summary>
    public Candle(DateTime fromEt, DateTime untilEt)
    {
        ValidateWindow(fromEt, untilEt);

        FromET  = fromEt;
        UntilET = untilEt;
    }

    /// <summary>
    /// Constructs a fully-formed candle from pre-computed OHLCV values. Useful for loading
    /// candles from external sources (a CSV row, a database, an existing aggregation, etc.).
    /// </summary>
    public Candle(
        DateTime fromEt, DateTime untilEt,
        double open, double high, double low, double close,
        int volume, int tickCount)
    {
        ValidateWindow(fromEt, untilEt);

        if (low > high)
            throw new ArgumentException("Low must be ≤ High.", nameof(low));

        if (open < low || open > high)
            throw new ArgumentOutOfRangeException(nameof(open), "Open must be within [Low, High].");

        if (close < low || close > high)
            throw new ArgumentOutOfRangeException(nameof(close), "Close must be within [Low, High].");

        if (volume < 0)
            throw new ArgumentOutOfRangeException(nameof(volume), "Volume cannot be negative.");

        if (tickCount < 0)
            throw new ArgumentOutOfRangeException(nameof(tickCount), "TickCount cannot be negative.");

        FromET  = fromEt;
        UntilET = untilEt;
        Open      = open;
        High      = high;
        Low       = low;
        Close     = close;
        Volume    = volume;
        TickCount = tickCount;
    }

    /// <summary>
    /// Records a trade at <paramref name="price"/> with <paramref name="size"/>. Updates
    /// O/H/L/C (first trade sets Open and seeds H/L) and accumulates Volume / TickCount.
    /// Does not validate the trade's timestamp — call <see cref="TryAdd"/> for that.
    /// </summary>
    public void AddTrade(double price, int size)
    {
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive.");

        if (TickCount == 0)
        {
            Open  = price;
            High  = price;
            Low   = price;
            Close = price;
        }
        else
        {
            if (price > High) High = price;
            if (price < Low)  Low  = price;
            Close = price;
        }

        Volume += size;
        TickCount++;
    }

    /// <summary>
    /// Records <paramref name="tick"/> only if it is a Trade tick (TradeBid or TradeAsk)
    /// whose <see cref="Tick.OnET"/> falls within <c>[FromET, UntilET)</c>. Returns true
    /// if applied, false otherwise (silent skip on non-trade or out-of-window).
    /// </summary>
    public bool TryAdd(Tick tick)
    {
        if ((tick.Kind & PriceKind.Trade) == 0)
            return false;

        if (tick.OnET < FromET || tick.OnET >= UntilET)
            return false;

        AddTrade(tick.Price, tick.Volume);

        return true;
    }

    private static void ValidateWindow(DateTime from, DateTime until)
    {
        if (from.Kind != DateTimeKind.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(from), "\"from.Kind\" must be \"Unspecified\".");

        if (until.Kind != DateTimeKind.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(until), "\"until.Kind\" must be \"Unspecified\".");

        if (from >= until)
            throw new ArgumentException("\"from\" must be earlier than \"until\".", nameof(from));
    }
}
