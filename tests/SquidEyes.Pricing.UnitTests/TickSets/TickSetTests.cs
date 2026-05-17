using System.Collections;
using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.TickSets;

public class TickSetTests
{
    private static readonly DateOnly Feb2 = new(2026, 2, 2);
    private static readonly Contract H26_ES = Contract.Create(Symbol.ES, "H26");

    [Fact]
    public void NonGenericEnumerator_YieldsSameTicks()
    {
        var builder = TickSet.CreateBuilder(Symbol.ES, Feb2, H26_ES);
        builder.Add(28_800_000, PriceKind.Bid, 6900.00m, 10);
        builder.Add(28_800_004, PriceKind.Ask, 6900.25m, 15);
        var ts = builder.Build();

        var generic = ts.ToList();

        var nonGeneric = new List<Tick>();
        foreach (Tick tick in (IEnumerable)ts)
            nonGeneric.Add(tick);

        Assert.Equal(generic, nonGeneric);
    }

    [Fact]
    public void Builder_AddByPriceTicks_BypassesTickSizeDivision()
    {
        var builder = TickSet.CreateBuilder(Symbol.ES, Feb2, H26_ES);

        // 23600 ticks * 0.25 tickSize = 5900.00 price
        builder.Add(28_800_000, PriceKind.Bid, priceTicks: 23600, size: 7);
        var ts = builder.Build();

        var tick = ts.Single();
        Assert.Equal(5900.00, tick.Price);
        Assert.Equal(7, tick.Volume);
        Assert.Equal(PriceKind.Bid, tick.Kind);
    }

    [Fact]
    public void Properties_ExposeConstructorArguments()
    {
        var ts = TickSet.CreateBuilder(Symbol.NQ, Feb2, Contract.Create(Symbol.NQ, "H26")).Build();

        Assert.Equal(Symbol.NQ, ts.Instrument.Symbol);
        Assert.Equal(Feb2, ts.Date);
        Assert.Equal("H26", ts.Contract.Code);
        Assert.Equal(0, ts.Count);
    }
}
