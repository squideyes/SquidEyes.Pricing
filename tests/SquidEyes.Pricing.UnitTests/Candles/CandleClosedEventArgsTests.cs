using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Candles;

public class CandleClosedEventArgsTests
{
    private static readonly DateTime From  = new(2026, 3, 2, 9, 30, 0);
    private static readonly DateTime Until = new(2026, 3, 2, 9, 31, 0);
    private static readonly DateTime Trigger = new(2026, 3, 2, 9, 31, 1);

    [Fact]
    public void Ctor_AssignsBothProperties()
    {
        var candle = new Candle(From, Until);
        var args   = new CandleClosedEventArgs(candle, Trigger);

        Assert.Same(candle, args.ClosedCandle);
        Assert.Equal(Trigger, args.TriggerET);
    }

    [Fact]
    public void Ctor_NullCandle_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new CandleClosedEventArgs(null!, Trigger));

    [Fact]
    public void Ctor_NonUnspecifiedTriggerKind_Throws()
    {
        var candle = new Candle(From, Until);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CandleClosedEventArgs(candle, DateTime.UtcNow));
    }
}
