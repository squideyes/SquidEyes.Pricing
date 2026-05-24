namespace SquidEyes.Pricing;

/// <summary>
/// Time-based <see cref="CandleSet"/>: bars are bucketed into fixed-length intervals
/// (e.g. 60s = one-minute candles). Bars are wall-clock aligned via floor-to-interval
/// (so 60s bars start at :00, 300s bars start at :00, :05, :10, etc.).
///
/// <see cref="CandleSet.Current"/> is the still-mutating bar; it closes (and a new one
/// is created) when a tick arrives with <c>OnET &gt;= Current.UntilET</c>.
/// </summary>
public sealed class IntervalCandleSet : CandleSet
{
    private readonly int _intervalSeconds;

    public IntervalCandleSet(int intervalSeconds, int capacity)
        : base(capacity)
    {
        if (intervalSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Interval must be positive.");

        _intervalSeconds = intervalSeconds;
    }

    public int IntervalSeconds => _intervalSeconds;

    public override void ProcessTick(Tick tick)
    {
        if (tick.OnET.Kind != DateTimeKind.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(tick), "\"tick.OnET.Kind\" must be \"Unspecified\".");

        if ((tick.Kind & PriceKind.Trade) == 0)
            return;

        var current = Current;

        if (current == null || tick.OnET >= current.UntilET)
        {
            if (current != null)
                RaiseClosed(current, tick.OnET);

            var from  = FloorToInterval(tick.OnET);
            var until = from.AddSeconds(_intervalSeconds);
            var bar   = new Candle(from, until);

            bar.AddTrade(tick.Price, tick.Volume);

            PushFront(bar);
        }
        else
        {
            current.AddTrade(tick.Price, tick.Volume);
        }
    }

    private DateTime FloorToInterval(DateTime time)
    {
        var intervalTicks = _intervalSeconds * TimeSpan.TicksPerSecond;
        var floored = (time.Ticks / intervalTicks) * intervalTicks;

        return new DateTime(floored, DateTimeKind.Unspecified);
    }
}
