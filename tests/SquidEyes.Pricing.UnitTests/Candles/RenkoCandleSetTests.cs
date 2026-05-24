using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Candles;

public class RenkoCandleSetTests
{
    // ES: tick size 0.25, so brickTicks=4 → brick size 1.00 (clean math for tests)
    private static readonly Instrument ES = Instrument.Create(Symbol.ES);

    private static readonly DateTime D = new(2026, 3, 2);

    private static Tick Trade(int h, int m, int s, double price, int size = 1) =>
        new(new DateTime(D.Year, D.Month, D.Day, h, m, s), PriceKind.TradeAsk, price, size);

    // ── Construction ─────────────────────────────────────────────────────

    [Fact]
    public void Ctor_AssignsProps()
    {
        var set = new RenkoCandleSet(ES, brickTicks: 4, capacity: 50, withWicks: true);

        Assert.Same(ES, set.Instrument);
        Assert.Equal(4, set.BrickTicks);
        Assert.Equal(1.00, set.BrickSize);
        Assert.True(set.WithWicks);
        Assert.Equal(50, set.Capacity);
        Assert.Equal(0, set.Count);
        Assert.Null(set.Current);
    }

    [Fact]
    public void Ctor_NullInstrument_Throws() =>
        Assert.Throws<ArgumentNullException>(
            () => new RenkoCandleSet(null!, 4, 10, false));

    [Fact]
    public void Ctor_NonPositiveBrickTicks_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RenkoCandleSet(ES, 0, 10, false));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RenkoCandleSet(ES, -1, 10, false));
    }

    [Fact]
    public void Ctor_NonPositiveCapacity_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RenkoCandleSet(ES, 4, 0, false));

    [Fact]
    public void ProcessTick_NonUnspecifiedKind_Throws()
    {
        var set  = new RenkoCandleSet(ES, 4, 10, false);
        var tick = new Tick(DateTime.UtcNow, PriceKind.TradeAsk, 100, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => set.ProcessTick(tick));
    }

    // ── Bootstrap ────────────────────────────────────────────────────────

    [Fact]
    public void ProcessTick_FirstTick_NoBrickEmitted()
    {
        var set = new RenkoCandleSet(ES, 4, 10, false);
        set.ProcessTick(Trade(9, 30, 0, 5000.00));

        Assert.Equal(0, set.Count);
        Assert.Null(set.Current);
    }

    [Fact]
    public void ProcessTick_NonTradeTicks_Ignored()
    {
        var set  = new RenkoCandleSet(ES, 4, 10, false);
        var bid  = new Tick(new DateTime(D.Year, D.Month, D.Day, 9, 30, 0), PriceKind.Bid, 5000.00, 1);
        var ask  = new Tick(new DateTime(D.Year, D.Month, D.Day, 9, 30, 1), PriceKind.Ask, 5001.00, 1);

        set.ProcessTick(bid);
        set.ProcessTick(ask);

        Assert.Equal(0, set.Count);
    }

    // ── Without wicks ────────────────────────────────────────────────────

    [Fact]
    public void NoWicks_UpContinuation_BodyOnly()
    {
        var set = new RenkoCandleSet(ES, 4, 10, false);

        set.ProcessTick(Trade(9, 30, 0, 5000.00));   // anchor
        set.ProcessTick(Trade(9, 30, 5, 5001.00));   // up brick

        Assert.Equal(1, set.Count);
        var b = set.Current!;
        Assert.Equal(5000.00, b.Open);
        Assert.Equal(5001.00, b.High);
        Assert.Equal(5000.00, b.Low);
        Assert.Equal(5001.00, b.Close);
    }

    [Fact]
    public void NoWicks_DownContinuation_BodyOnly()
    {
        var set = new RenkoCandleSet(ES, 4, 10, false);

        set.ProcessTick(Trade(9, 30, 0, 5000.00));
        set.ProcessTick(Trade(9, 30, 5, 4999.00));

        var b = set.Current!;
        Assert.Equal(5000.00, b.Open);
        Assert.Equal(5000.00, b.High);
        Assert.Equal(4999.00, b.Low);
        Assert.Equal(4999.00, b.Close);
    }

    [Fact]
    public void NoWicks_PriceWithinBody_NoBrick()
    {
        var set = new RenkoCandleSet(ES, 4, 10, false);

        set.ProcessTick(Trade(9, 30, 0, 5000.00));
        set.ProcessTick(Trade(9, 30, 5, 5000.75));   // less than +1
        set.ProcessTick(Trade(9, 30, 6, 4999.25));   // greater than -1

        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void NoWicks_UpChain_EmitsMultipleBricks_NewestFirst()
    {
        var set = new RenkoCandleSet(ES, 4, 10, false);

        set.ProcessTick(Trade(9, 30, 0, 5000.00));
        set.ProcessTick(Trade(9, 30, 5, 5003.50));   // up by 3 → 3 bricks

        Assert.Equal(3, set.Count);
        Assert.Equal(5002.00, set[0].Open);
        Assert.Equal(5003.00, set[0].Close);
        Assert.Equal(5001.00, set[1].Open);
        Assert.Equal(5002.00, set[1].Close);
        Assert.Equal(5000.00, set[2].Open);
        Assert.Equal(5001.00, set[2].Close);
    }

    [Fact]
    public void NoWicks_DownChain_EmitsMultipleBricks()
    {
        var set = new RenkoCandleSet(ES, 4, 10, false);

        set.ProcessTick(Trade(9, 30, 0, 5000.00));
        set.ProcessTick(Trade(9, 30, 5, 4996.90));   // crosses 4999, 4998, 4997 → 3 bricks

        Assert.Equal(3, set.Count);
        Assert.Equal(4998.00, set[0].Open);
        Assert.Equal(4997.00, set[0].Close);
        Assert.Equal(4999.00, set[1].Open);
        Assert.Equal(4998.00, set[1].Close);
        Assert.Equal(5000.00, set[2].Open);
        Assert.Equal(4999.00, set[2].Close);
    }

    // ── With wicks ───────────────────────────────────────────────────────

    [Fact]
    public void Wicks_UpBrick_LowerWickFromPriorDip()
    {
        var set = new RenkoCandleSet(ES, 4, 10, true);

        set.ProcessTick(Trade(9, 30, 0, 5000.00));   // anchor
        set.ProcessTick(Trade(9, 30, 1, 4999.50));   // dip below anchor — no brick
        set.ProcessTick(Trade(9, 30, 2, 5001.00));   // up brick

        var b = set.Current!;
        Assert.Equal(5000.00, b.Open);
        Assert.Equal(5001.00, b.Close);
        Assert.Equal(5001.00, b.High);
        Assert.Equal(4999.50, b.Low);   // wick captures the dip
    }

    [Fact]
    public void Wicks_DownBrick_UpperWickFromPriorSpike()
    {
        var set = new RenkoCandleSet(ES, 4, 10, true);

        set.ProcessTick(Trade(9, 30, 0, 5000.00));
        set.ProcessTick(Trade(9, 30, 1, 5000.80));   // spike up — no brick
        set.ProcessTick(Trade(9, 30, 2, 4999.00));   // down brick

        var b = set.Current!;
        Assert.Equal(5000.00, b.Open);
        Assert.Equal(4999.00, b.Close);
        Assert.Equal(5000.80, b.High);   // wick captures spike
        Assert.Equal(4999.00, b.Low);
    }

    [Fact]
    public void Wicks_UpChain_FirstHasLowerWick_LastHasUpperWick_InteriorPlain()
    {
        var set = new RenkoCandleSet(ES, 4, 10, true);

        set.ProcessTick(Trade(9, 30, 0, 5000.00));
        set.ProcessTick(Trade(9, 30, 1, 4999.50));   // wick dip
        set.ProcessTick(Trade(9, 30, 2, 5002.75));   // up by ~3 (2 bricks: 5000→5001, 5001→5002)
                                                     // wait — 5002.75 >= 5002, so 2 bricks. Need 5003 for 3.

        // 5002.75 satisfies 5002.75 >= 5000+2 (=5002) but not >= 5003 → 2 bricks
        Assert.Equal(2, set.Count);

        var first = set[1];   // chronologically first brick
        Assert.Equal(5000.00, first.Open);
        Assert.Equal(5001.00, first.Close);
        Assert.Equal(5001.00, first.High);
        Assert.Equal(4999.50, first.Low);    // first gets lower wick

        var last = set[0];    // chronologically last brick
        Assert.Equal(5001.00, last.Open);
        Assert.Equal(5002.00, last.Close);
        Assert.Equal(5002.75, last.High);    // last gets upper wick
        Assert.Equal(5001.00, last.Low);
    }

    [Fact]
    public void Wicks_DownChain_FirstHasUpperWick_LastHasLowerWick()
    {
        var set = new RenkoCandleSet(ES, 4, 10, true);

        set.ProcessTick(Trade(9, 30, 0, 5000.00));
        set.ProcessTick(Trade(9, 30, 1, 5000.80));   // wick spike
        set.ProcessTick(Trade(9, 30, 2, 4996.90));   // crosses 4999, 4998, 4997 → 3 bricks

        Assert.Equal(3, set.Count);

        var first  = set[2];
        var middle = set[1];
        var last   = set[0];

        Assert.Equal(5000.00, first.Open);
        Assert.Equal(4999.00, first.Close);
        Assert.Equal(5000.80, first.High);   // first down-chain → upper wick
        Assert.Equal(4999.00, first.Low);

        Assert.Equal(4999.00, middle.Open);
        Assert.Equal(4998.00, middle.Close);
        Assert.Equal(4999.00, middle.High);   // interior: body only
        Assert.Equal(4998.00, middle.Low);

        Assert.Equal(4998.00, last.Open);
        Assert.Equal(4997.00, last.Close);
        Assert.Equal(4998.00, last.High);
        Assert.Equal(4996.90, last.Low);     // last down-chain → lower wick
    }

    // ── Capacity / eviction ──────────────────────────────────────────────

    [Fact]
    public void Capacity_EvictsOldestBrick()
    {
        var set = new RenkoCandleSet(ES, 4, capacity: 2, withWicks: false);

        set.ProcessTick(Trade(9, 30, 0, 5000.00));
        set.ProcessTick(Trade(9, 30, 1, 5004.10));   // 4 bricks → keep 2 newest

        Assert.Equal(2, set.Count);
        Assert.Equal(5004.00, set[0].Close);
        Assert.Equal(5003.00, set[1].Close);
    }

    // ── Event firing ─────────────────────────────────────────────────────

    [Fact]
    public void CandleClosed_FiresOncePerBrick()
    {
        var set    = new RenkoCandleSet(ES, 4, 10, false);
        var closed = new List<Candle>();

        set.CandleClosed += (_, e) => closed.Add(e.ClosedCandle);

        set.ProcessTick(Trade(9, 30, 0, 5000.00));
        set.ProcessTick(Trade(9, 30, 1, 5002.50));   // 2 bricks

        Assert.Equal(2, closed.Count);
        Assert.Equal(5001.00, closed[0].Close);
        Assert.Equal(5002.00, closed[1].Close);
    }

    // ── Window invariants ────────────────────────────────────────────────

    [Fact]
    public void Brick_Windows_StrictlyOrdered_NoOverlap_PositiveDuration()
    {
        var set = new RenkoCandleSet(ES, 4, 10, false);

        set.ProcessTick(Trade(9, 30, 0, 5000.00));
        set.ProcessTick(Trade(9, 30, 1, 5003.10));   // chain of 3 at single tick

        Assert.Equal(3, set.Count);

        var snap = set.AsReadOnly();

        // Iterate chronologically (oldest to newest = index Count-1 down to 0)
        for (var i = snap.Count - 1; i >= 0; i--)
        {
            Assert.True(snap[i].FromET < snap[i].UntilET);

            if (i < snap.Count - 1)
                Assert.True(snap[i].FromET >= snap[i + 1].UntilET);
        }
    }

    // ── Reversal ─────────────────────────────────────────────────────────

    [Fact]
    public void Reversal_ImmediateDownAfterUp_Works()
    {
        var set = new RenkoCandleSet(ES, 4, 10, false);

        set.ProcessTick(Trade(9, 30, 0, 5000.00));   // anchor
        set.ProcessTick(Trade(9, 30, 1, 5001.00));   // up brick (close=5001)
        set.ProcessTick(Trade(9, 30, 2, 5000.00));   // down brick (close=5000) — moved B from last close

        Assert.Equal(2, set.Count);
        Assert.Equal(5001.00, set[0].Open);
        Assert.Equal(5000.00, set[0].Close);
        Assert.Equal(5000.00, set[1].Open);
        Assert.Equal(5001.00, set[1].Close);
    }

    // ── Mixed instrument tick size ───────────────────────────────────────

    [Fact]
    public void CL_TickSize_001_BrickSize_BrickTicks10_PriceMath()
    {
        var cl  = Instrument.Create(Symbol.CL);   // tick 0.01
        var set = new RenkoCandleSet(cl, brickTicks: 10, capacity: 10, withWicks: false);

        Assert.Equal(0.10, set.BrickSize);

        set.ProcessTick(Trade(9, 30, 0, 75.00));
        set.ProcessTick(Trade(9, 30, 1, 75.10));

        Assert.Equal(1, set.Count);
        Assert.Equal(75.00, set.Current!.Open);
        Assert.Equal(75.10, set.Current.Close);
    }
}
