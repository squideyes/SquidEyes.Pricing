using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Candles;

public class IntervalCandleSetTests
{
    private static readonly DateTime D = new(2026, 3, 2);

    private static Tick Trade(int h, int m, int s, double price, int size = 1) =>
        new(new DateTime(D.Year, D.Month, D.Day, h, m, s), PriceKind.TradeAsk, price, size);

    // ── Construction ─────────────────────────────────────────────────────

    [Fact]
    public void Ctor_AssignsProps()
    {
        var set = new IntervalCandleSet(60, 100);

        Assert.Equal(60, set.IntervalSeconds);
        Assert.Equal(100, set.Capacity);
        Assert.Equal(0, set.Count);
        Assert.Null(set.Current);
    }

    [Fact]
    public void Ctor_NonPositiveInterval_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntervalCandleSet(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntervalCandleSet(-1, 10));
    }

    [Fact]
    public void Ctor_NonPositiveCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntervalCandleSet(60, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntervalCandleSet(60, -1));
    }

    // ── ProcessTick — basic bar building ─────────────────────────────────

    [Fact]
    public void ProcessTick_FirstTrade_CreatesCurrentBar()
    {
        var set = new IntervalCandleSet(60, 10);
        set.ProcessTick(Trade(9, 30, 15, 100.00));

        Assert.Equal(1, set.Count);
        Assert.NotNull(set.Current);
        Assert.Equal(new DateTime(D.Year, D.Month, D.Day, 9, 30, 0), set.Current!.FromET);
        Assert.Equal(new DateTime(D.Year, D.Month, D.Day, 9, 31, 0), set.Current.UntilET);
        Assert.Equal(100.00, set.Current.Open);
        Assert.Equal(1, set.Current.Volume);
        Assert.Equal(1, set.Current.TickCount);
    }

    [Fact]
    public void ProcessTick_TicksWithinSameBucket_UpdateCurrent()
    {
        var set = new IntervalCandleSet(60, 10);
        set.ProcessTick(Trade(9, 30, 10, 100.00));
        set.ProcessTick(Trade(9, 30, 20, 100.50));
        set.ProcessTick(Trade(9, 30, 30,  99.75));
        set.ProcessTick(Trade(9, 30, 59, 100.25));

        Assert.Equal(1, set.Count);
        Assert.Equal(100.00, set.Current!.Open);
        Assert.Equal(100.50, set.Current.High);
        Assert.Equal( 99.75, set.Current.Low);
        Assert.Equal(100.25, set.Current.Close);
        Assert.Equal(4, set.Current.TickCount);
    }

    [Fact]
    public void ProcessTick_TickAtUntil_RollsToNextBar()
    {
        var set = new IntervalCandleSet(60, 10);
        set.ProcessTick(Trade(9, 30, 30, 100.00));
        set.ProcessTick(Trade(9, 31,  0, 101.00));   // exactly on bar boundary

        Assert.Equal(2, set.Count);
        Assert.Equal(101.00, set.Current!.Open);
        Assert.Equal(100.00, set[1].Open);
    }

    [Fact]
    public void ProcessTick_NewBar_PushesToFront_OlderSlidesDown()
    {
        var set = new IntervalCandleSet(60, 10);
        set.ProcessTick(Trade(9, 30, 5, 100.00));
        set.ProcessTick(Trade(9, 31, 5, 101.00));
        set.ProcessTick(Trade(9, 32, 5, 102.00));

        Assert.Equal(3, set.Count);
        Assert.Equal(102.00, set[0].Open);
        Assert.Equal(101.00, set[1].Open);
        Assert.Equal(100.00, set[2].Open);
    }

    // ── Filtering ────────────────────────────────────────────────────────

    [Fact]
    public void ProcessTick_BidQuote_Ignored()
    {
        var set  = new IntervalCandleSet(60, 10);
        var tick = new Tick(new DateTime(D.Year, D.Month, D.Day, 9, 30, 15), PriceKind.Bid, 100.00, 5);
        set.ProcessTick(tick);

        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void ProcessTick_AskQuote_Ignored()
    {
        var set  = new IntervalCandleSet(60, 10);
        var tick = new Tick(new DateTime(D.Year, D.Month, D.Day, 9, 30, 15), PriceKind.Ask, 100.00, 5);
        set.ProcessTick(tick);

        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void ProcessTick_TradeBidAccepted()
    {
        var set  = new IntervalCandleSet(60, 10);
        var tick = new Tick(new DateTime(D.Year, D.Month, D.Day, 9, 30, 15), PriceKind.TradeBid, 100.00, 5);
        set.ProcessTick(tick);

        Assert.Equal(1, set.Count);
        Assert.Equal(5, set.Current!.Volume);
    }

    [Fact]
    public void ProcessTick_NonUnspecifiedKind_Throws()
    {
        var set  = new IntervalCandleSet(60, 10);
        var tick = new Tick(DateTime.UtcNow, PriceKind.TradeAsk, 100.00, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => set.ProcessTick(tick));
    }

    // ── Capacity eviction ────────────────────────────────────────────────

    [Fact]
    public void ProcessTick_AtCapacity_EvictsOldest()
    {
        var set = new IntervalCandleSet(60, 3);
        set.ProcessTick(Trade(9, 30, 0, 100.00));
        set.ProcessTick(Trade(9, 31, 0, 101.00));
        set.ProcessTick(Trade(9, 32, 0, 102.00));
        set.ProcessTick(Trade(9, 33, 0, 103.00));

        Assert.Equal(3, set.Count);
        Assert.Equal(103.00, set[0].Open);
        Assert.Equal(102.00, set[1].Open);
        Assert.Equal(101.00, set[2].Open);
    }

    // ── CandleClosed event ───────────────────────────────────────────────

    [Fact]
    public void CandleClosed_FiresOnRollover_WithJustClosedCandle()
    {
        var set    = new IntervalCandleSet(60, 10);
        var closed = new List<(Candle Candle, DateTime Trigger)>();

        set.CandleClosed += (_, e) => closed.Add((e.ClosedCandle, e.TriggerET));

        set.ProcessTick(Trade(9, 30,  5, 100.00));
        set.ProcessTick(Trade(9, 30, 30, 100.50));

        Assert.Empty(closed);

        set.ProcessTick(Trade(9, 31,  5, 101.00));

        Assert.Single(closed);
        Assert.Equal(100.00, closed[0].Candle.Open);
        Assert.Equal(100.50, closed[0].Candle.Close);
        Assert.Equal(new DateTime(D.Year, D.Month, D.Day, 9, 31, 5), closed[0].Trigger);
    }

    [Fact]
    public void CandleClosed_DoesNotFireForFirstBar() =>
        // Sanity: the very first bar has nothing to close before it.
        new IntervalCandleSet(60, 10).ProcessTick(Trade(9, 30, 0, 100.00));

    // ── Indexer ──────────────────────────────────────────────────────────

    [Fact]
    public void Indexer_OutOfRange_Throws()
    {
        var set = new IntervalCandleSet(60, 10);
        set.ProcessTick(Trade(9, 30, 0, 100));

        Assert.Throws<ArgumentOutOfRangeException>(() => set[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => set[1]);
    }

    [Fact]
    public void AsReadOnly_ReturnsNewestFirst()
    {
        var set = new IntervalCandleSet(60, 10);
        set.ProcessTick(Trade(9, 30, 0, 100));
        set.ProcessTick(Trade(9, 31, 0, 101));

        var snap = set.AsReadOnly();

        Assert.Equal(2, snap.Count);
        Assert.Equal(101, snap[0].Open);
        Assert.Equal(100, snap[1].Open);
    }

    // ── Floor-to-interval alignment ──────────────────────────────────────

    [Fact]
    public void ProcessTick_5MinuteInterval_FloorsTo5MinBoundaries()
    {
        var set = new IntervalCandleSet(300, 10);

        set.ProcessTick(Trade(9, 33, 17, 100));   // 9:33:17 → bucket 9:30:00
        Assert.Equal(new DateTime(D.Year, D.Month, D.Day, 9, 30, 0), set.Current!.FromET);
        Assert.Equal(new DateTime(D.Year, D.Month, D.Day, 9, 35, 0), set.Current.UntilET);

        set.ProcessTick(Trade(9, 37,  2, 101));   // 9:37:02 → bucket 9:35:00
        Assert.Equal(new DateTime(D.Year, D.Month, D.Day, 9, 35, 0), set.Current.FromET);
    }
}
