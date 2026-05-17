using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Instruments;

public class InstrumentTests
{
    [Theory]
    [InlineData(Symbol.ES, 0.25)]
    [InlineData(Symbol.NQ, 0.25)]
    [InlineData(Symbol.CL, 0.01)]
    [InlineData(Symbol.GC, 0.10)]
    public void Create_ReturnsCorrectTickSize(Symbol symbol, decimal expectedTickSize)
    {
        var instrument = Instrument.Create(symbol);
        Assert.Equal(expectedTickSize, instrument.TickSize);
        Assert.Equal(symbol, instrument.Symbol);
    }

    [Theory]
    [InlineData(Symbol.ES)]
    [InlineData(Symbol.NQ)]
    [InlineData(Symbol.CL)]
    [InlineData(Symbol.GC)]
    [InlineData(Symbol.TY)]
    [InlineData(Symbol.FV)]
    [InlineData(Symbol.US)]
    [InlineData(Symbol.JY)]
    [InlineData(Symbol.EU)]
    [InlineData(Symbol.BP)]
    public void Create_KindIsFuture(Symbol symbol)
    {
        var instrument = Instrument.Create(symbol);
        Assert.Equal(InstrumentKind.Future, instrument.Kind);
    }

    [Fact]
    public void Create_ReturnsCachedInstance()
    {
        var a1 = Instrument.Create(Symbol.ES);
        var a2 = Instrument.Create(Symbol.ES);
        Assert.Same(a1, a2);
    }

    [Fact]
    public void ImplicitConversion_FromSymbol()
    {
        Instrument instrument = Symbol.NQ;
        Assert.Equal(Symbol.NQ, instrument.Symbol);
    }

    [Fact]
    public void Parse_ValidCode_ReturnsInstrument()
    {
        var instrument = Instrument.Parse("ES");
        Assert.Equal(Symbol.ES, instrument.Symbol);
    }

    [Fact]
    public void Parse_CaseInsensitive()
    {
        var instrument = Instrument.Parse("es");
        Assert.Equal(Symbol.ES, instrument.Symbol);
    }

    [Fact]
    public void Parse_InvalidCode_Throws()
    {
        Assert.Throws<ArgumentException>(() => Instrument.Parse("XX"));
    }

    [Fact]
    public void IsSupported_ValidCode_True()
    {
        Assert.True(Instrument.IsSupported("NQ"));
    }

    [Fact]
    public void IsSupported_InvalidCode_False()
    {
        Assert.False(Instrument.IsSupported("XX"));
    }

    [Fact]
    public void ToString_ReturnsSymbolName()
    {
        Assert.Equal("ES", Instrument.Create(Symbol.ES).ToString());
    }

    [Theory]
    [InlineData(Symbol.ES, 50.0)]
    [InlineData(Symbol.NQ, 20.0)]
    [InlineData(Symbol.CL, 1000.0)]
    [InlineData(Symbol.GC, 100.0)]
    [InlineData(Symbol.TY, 1000.0)]
    [InlineData(Symbol.FV, 1000.0)]
    [InlineData(Symbol.US, 1000.0)]
    [InlineData(Symbol.JY, 125000.0)]
    [InlineData(Symbol.EU, 125000.0)]
    [InlineData(Symbol.BP, 62500.0)]
    public void PointValue_MatchesSpec(Symbol symbol, double expectedPointValue)
    {
        var instrument = Instrument.Create(symbol);
        Assert.Equal(expectedPointValue, instrument.PointValue);
    }

    [Theory]
    [InlineData(Symbol.ES, 4)]      // 1.00 / 0.25
    [InlineData(Symbol.CL, 100)]    // 1.00 / 0.01
    [InlineData(Symbol.GC, 10)]     // 1.00 / 0.10
    public void TicksPerPoint_MatchesSpec(Symbol symbol, int expected)
    {
        var instrument = Instrument.Create(symbol);
        Assert.Equal(expected, instrument.TicksPerPoint);
    }

    [Theory]
    [InlineData(5900.13, 5900.25)]   // 5900.13 / 0.25 = 23600.52 → round to 23601 → 5900.25
    [InlineData(5900.12, 5900.00)]   // 5900.12 / 0.25 = 23600.48 → round to 23600 → 5900.00
    [InlineData(5900.00, 5900.00)]   // exact tick
    [InlineData(5900.50, 5900.50)]   // exact tick
    public void Round_SnapsToNearestTick(double input, double expected)
    {
        var es = Instrument.Create(Symbol.ES);   // 0.25 tick
        Assert.Equal(expected, es.Round(input));
    }
}
