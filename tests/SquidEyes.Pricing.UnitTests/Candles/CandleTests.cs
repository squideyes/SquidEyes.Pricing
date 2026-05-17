using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Candles;

public class CandleTests
{
    private static readonly DateTime From  = new(2026, 3, 2, 9, 30, 0);
    private static readonly DateTime Until = new(2026, 3, 2, 9, 31, 0);

    // ── EmptyCandle constructor ──────────────────────────────────────────

    [Fact]
    public void EmptyCtor_SetsWindow_AndIsEmpty()
    {
        var c = new Candle(From, Until);

        Assert.Equal(From,  c.FromET);
        Assert.Equal(Until, c.UntilET);
        Assert.True(c.IsEmpty);
        Assert.Equal(0, c.TickCount);
        Assert.Equal(0, c.Volume);
        Assert.Equal(0.0, c.Open);
        Assert.Equal(0.0, c.High);
        Assert.Equal(0.0, c.Low);
        Assert.Equal(0.0, c.Close);
    }

    [Fact]
    public void EmptyCtor_FromAfterUntil_Throws() =>
        Assert.Throws<ArgumentException>(() => new Candle(Until, From));

    [Fact]
    public void EmptyCtor_FromEqualsUntil_Throws() =>
        Assert.Throws<ArgumentException>(() => new Candle(From, From));

    [Fact]
    public void EmptyCtor_NonUnspecifiedFromKind_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Candle(DateTime.UtcNow, Until));

    [Fact]
    public void EmptyCtor_NonUnspecifiedUntilKind_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Candle(From, DateTime.UtcNow));

    // ── OHLCV constructor ────────────────────────────────────────────────

    [Fact]
    public void OhlcvCtor_LoadsAllValues()
    {
        var c = new Candle(From, Until,
            open: 5900.00, high: 5901.25, low: 5899.75, close: 5900.50,
            volume: 250, tickCount: 47);

        Assert.Equal(5900.00, c.Open);
        Assert.Equal(5901.25, c.High);
        Assert.Equal(5899.75, c.Low);
        Assert.Equal(5900.50, c.Close);
        Assert.Equal(250, c.Volume);
        Assert.Equal(47, c.TickCount);
        Assert.False(c.IsEmpty);
    }

    [Fact]
    public void OhlcvCtor_LowAboveHigh_Throws() =>
        Assert.Throws<ArgumentException>(
            () => new Candle(From, Until, 100, 99, 101, 100, 1, 1));

    [Fact]
    public void OhlcvCtor_OpenAboveHigh_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Candle(From, Until, 102, 101, 99, 100, 1, 1));

    [Fact]
    public void OhlcvCtor_OpenBelowLow_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Candle(From, Until, 98, 101, 99, 100, 1, 1));

    [Fact]
    public void OhlcvCtor_CloseAboveHigh_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Candle(From, Until, 100, 101, 99, 102, 1, 1));

    [Fact]
    public void OhlcvCtor_CloseBelowLow_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Candle(From, Until, 100, 101, 99, 98, 1, 1));

    [Fact]
    public void OhlcvCtor_NegativeVolume_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Candle(From, Until, 100, 100, 100, 100, -1, 0));

    [Fact]
    public void OhlcvCtor_NegativeTickCount_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Candle(From, Until, 100, 100, 100, 100, 0, -1));

    [Fact]
    public void OhlcvCtor_FromAfterUntil_Throws() =>
        Assert.Throws<ArgumentException>(
            () => new Candle(Until, From, 100, 100, 100, 100, 0, 0));

    // ── AddTrade ─────────────────────────────────────────────────────────

    [Fact]
    public void AddTrade_FirstTick_SeedsOhlc()
    {
        var c = new Candle(From, Until);
        c.AddTrade(5900.25, 3);

        Assert.Equal(5900.25, c.Open);
        Assert.Equal(5900.25, c.High);
        Assert.Equal(5900.25, c.Low);
        Assert.Equal(5900.25, c.Close);
        Assert.Equal(3, c.Volume);
        Assert.Equal(1, c.TickCount);
        Assert.False(c.IsEmpty);
    }

    [Fact]
    public void AddTrade_SubsequentTicks_TrackHighLowClose()
    {
        var c = new Candle(From, Until);
        c.AddTrade(5900.00, 1);   // O=5900
        c.AddTrade(5901.25, 2);   // new high
        c.AddTrade(5899.75, 1);   // new low
        c.AddTrade(5900.50, 4);   // close

        Assert.Equal(5900.00, c.Open);
        Assert.Equal(5901.25, c.High);
        Assert.Equal(5899.75, c.Low);
        Assert.Equal(5900.50, c.Close);
        Assert.Equal(8, c.Volume);
        Assert.Equal(4, c.TickCount);
    }

    [Fact]
    public void AddTrade_HighNotExceeded_LeavesHigh()
    {
        var c = new Candle(From, Until);
        c.AddTrade(100, 1);
        c.AddTrade(99,  1);
        Assert.Equal(100, c.High);
        Assert.Equal(99,  c.Low);
        Assert.Equal(99,  c.Close);
    }

    [Fact]
    public void AddTrade_LowNotBettered_LeavesLow()
    {
        var c = new Candle(From, Until);
        c.AddTrade(100, 1);
        c.AddTrade(101, 1);
        Assert.Equal(101, c.High);
        Assert.Equal(100, c.Low);
        Assert.Equal(101, c.Close);
    }

    [Fact]
    public void AddTrade_NonPositiveSize_Throws()
    {
        var c = new Candle(From, Until);

        Assert.Throws<ArgumentOutOfRangeException>(() => c.AddTrade(100, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => c.AddTrade(100, -5));
    }

    // ── TryAdd ───────────────────────────────────────────────────────────

    [Fact]
    public void TryAdd_TradeBidInWindow_AppliesAndReturnsTrue()
    {
        var c = new Candle(From, Until);
        var tick = new Tick(new DateTime(2026, 3, 2, 9, 30, 15), PriceKind.TradeBid, 5900.25, 2);

        Assert.True(c.TryAdd(tick));
        Assert.Equal(5900.25, c.Open);
        Assert.Equal(2, c.Volume);
        Assert.Equal(1, c.TickCount);
    }

    [Fact]
    public void TryAdd_TradeAskInWindow_AppliesAndReturnsTrue()
    {
        var c = new Candle(From, Until);
        var tick = new Tick(new DateTime(2026, 3, 2, 9, 30, 30), PriceKind.TradeAsk, 5901.00, 1);

        Assert.True(c.TryAdd(tick));
        Assert.Equal(5901.00, c.Close);
        Assert.Equal(1, c.Volume);
    }

    [Fact]
    public void TryAdd_BidQuote_Skipped()
    {
        var c = new Candle(From, Until);
        var tick = new Tick(new DateTime(2026, 3, 2, 9, 30, 15), PriceKind.Bid, 5900.00, 10);

        Assert.False(c.TryAdd(tick));
        Assert.True(c.IsEmpty);
    }

    [Fact]
    public void TryAdd_AskQuote_Skipped()
    {
        var c = new Candle(From, Until);
        var tick = new Tick(new DateTime(2026, 3, 2, 9, 30, 15), PriceKind.Ask, 5900.25, 10);

        Assert.False(c.TryAdd(tick));
        Assert.True(c.IsEmpty);
    }

    [Fact]
    public void TryAdd_BeforeWindow_Skipped()
    {
        var c = new Candle(From, Until);
        var tick = new Tick(new DateTime(2026, 3, 2, 9, 29, 59), PriceKind.TradeBid, 5900.00, 1);

        Assert.False(c.TryAdd(tick));
        Assert.True(c.IsEmpty);
    }

    [Fact]
    public void TryAdd_AtFrom_Applied()
    {
        var c = new Candle(From, Until);
        var tick = new Tick(From, PriceKind.TradeBid, 5900.00, 1);

        Assert.True(c.TryAdd(tick));
    }

    [Fact]
    public void TryAdd_AtUntil_Skipped()
    {
        // Half-open: [From, Until) — Until itself is OUT
        var c = new Candle(From, Until);
        var tick = new Tick(Until, PriceKind.TradeBid, 5900.00, 1);

        Assert.False(c.TryAdd(tick));
        Assert.True(c.IsEmpty);
    }

    [Fact]
    public void TryAdd_AfterWindow_Skipped()
    {
        var c = new Candle(From, Until);
        var tick = new Tick(new DateTime(2026, 3, 2, 9, 31, 30), PriceKind.TradeAsk, 5900.00, 1);

        Assert.False(c.TryAdd(tick));
        Assert.True(c.IsEmpty);
    }
}
