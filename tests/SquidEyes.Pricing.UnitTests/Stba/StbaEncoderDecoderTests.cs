using SquidEyes.Pricing;
using SquidEyes.Pricing.Stba;

namespace SquidEyes.Pricing.UnitTests.Stba;

public class StbaEncoderDecoderTests
{
    private static readonly Instrument ES = Symbol.ES;
    private static readonly Instrument NQ = Symbol.NQ;
    private static readonly DateOnly Feb2 = new(2026, 2, 2);
    private static readonly DateOnly Jan15 = new(2026, 1, 15);
    private static readonly DateOnly Mar2 = new(2026, 3, 2);
    private static readonly Contract H26_ES = Contract.Create(Symbol.ES, "H26");
    private static readonly Contract H26_NQ = Contract.Create(Symbol.NQ, "H26");

    [Fact]
    public void Roundtrip_EmptyTickSet_PreservesMetadata()
    {
        var original = TickSet.CreateBuilder(ES, Feb2, H26_ES).Build();

        var decoded = RoundTrip(original);

        Assert.Equal(Symbol.ES, decoded.Instrument.Symbol);
        Assert.Equal(new DateOnly(2026, 2, 2), decoded.Date);
        Assert.Equal("H26", decoded.Contract.Code);
        Assert.Equal(0.25m, decoded.Instrument.TickSize);
        Assert.Equal(0, decoded.Count);
    }

    [Fact]
    public void Roundtrip_SingleTradeBid_PreservesData()
    {
        var builder = TickSet.CreateBuilder(NQ, Jan15, H26_NQ);
        builder.Add(28800000, PriceKind.TradeBid, 20000.25m, 5);
        var original = builder.Build();

        var decoded = RoundTrip(original);

        Assert.Equal(1, decoded.Count);
        var tick = decoded.First();
        Assert.Equal(new DateOnly(2026, 1, 15).ToDateTime(TimeOnly.MinValue).AddMilliseconds(28800000), tick.OnET);
        Assert.Equal(PriceKind.TradeBid, tick.Kind);
        Assert.Equal(20000.25, tick.Price);
        Assert.Equal(5, tick.Volume);
    }

    [Fact]
    public void Roundtrip_SingleTradeAsk_PreservesData()
    {
        var builder = TickSet.CreateBuilder(NQ, Jan15, H26_NQ);
        builder.Add(28800000, PriceKind.TradeAsk, 20000.50m, 3);
        var original = builder.Build();

        var decoded = RoundTrip(original);

        var tick = decoded.First();
        Assert.Equal(PriceKind.TradeAsk, tick.Kind);
        Assert.Equal(20000.50, tick.Price);
        Assert.Equal(3, tick.Volume);
    }

    [Fact]
    public void Roundtrip_MixedKinds_PreservesAllTicks()
    {
        var builder = TickSet.CreateBuilder(ES, Mar2, H26_ES);
        builder.Add(28800000, PriceKind.Bid, 6900.00m, 10);
        builder.Add(28800000, PriceKind.Ask, 6900.25m, 15);
        builder.Add(28800000, PriceKind.TradeAsk, 6900.25m, 3);
        builder.Add(28800004, PriceKind.Bid, 6900.25m, 20);
        builder.Add(28800004, PriceKind.Ask, 6900.50m, 25);
        builder.Add(28800100, PriceKind.TradeBid, 6900.00m, 7);
        var original = builder.Build();

        var decoded = RoundTrip(original);

        Assert.Equal(original.Count, decoded.Count);

        var origTicks = original.ToList();
        var decTicks = decoded.ToList();
        for (int i = 0; i < origTicks.Count; i++)
        {
            Assert.Equal(origTicks[i].OnET, decTicks[i].OnET);
            Assert.Equal(origTicks[i].Kind, decTicks[i].Kind);
            Assert.Equal(origTicks[i].Price, decTicks[i].Price);
            Assert.Equal(origTicks[i].Volume, decTicks[i].Volume);
        }
    }

    [Fact]
    public void Roundtrip_TradeBidAndTradeAsk_ShareDeltaStream()
    {
        // Both kinds use lastPriceT — encode a sequence and confirm round-trip prices.
        var builder = TickSet.CreateBuilder(ES, Feb2, H26_ES);
        builder.Add(28800000, PriceKind.TradeBid, 6900.00m, 1);
        builder.Add(28800004, PriceKind.TradeAsk, 6900.25m, 2);
        builder.Add(28800008, PriceKind.TradeBid, 6900.00m, 3);
        builder.Add(28800012, PriceKind.TradeAsk, 6901.00m, 4);
        var original = builder.Build();

        var decoded = RoundTrip(original);
        var ticks = decoded.ToList();
        Assert.Equal(4, ticks.Count);
        Assert.Equal(6900.00, ticks[0].Price);
        Assert.Equal(6900.25, ticks[1].Price);
        Assert.Equal(6900.00, ticks[2].Price);
        Assert.Equal(6901.00, ticks[3].Price);
        Assert.Equal(PriceKind.TradeBid, ticks[0].Kind);
        Assert.Equal(PriceKind.TradeAsk, ticks[1].Kind);
    }

    [Fact]
    public void Roundtrip_AggregatesDuplicateKeys()
    {
        var builder = TickSet.CreateBuilder(ES, Feb2, H26_ES);
        builder.Add(28800000, PriceKind.Bid, 6900.00m, 10);
        builder.Add(28800000, PriceKind.Bid, 6900.00m, 5);
        var original = builder.Build();

        Assert.Equal(1, original.Count);
        var tick = original.First();
        Assert.Equal(15, tick.Volume);

        var decoded = RoundTrip(original);
        Assert.Equal(1, decoded.Count);
        Assert.Equal(15, decoded.First().Volume);
    }

    [Fact]
    public void Roundtrip_NegativePriceDelta_HandledCorrectly()
    {
        var builder = TickSet.CreateBuilder(ES, Feb2, H26_ES);
        builder.Add(28800000, PriceKind.TradeBid, 6950.00m, 1);
        builder.Add(28800004, PriceKind.TradeBid, 6940.00m, 2);
        builder.Add(28800008, PriceKind.TradeBid, 6960.00m, 3);
        var original = builder.Build();

        var decoded = RoundTrip(original);
        var ticks = decoded.ToList();
        Assert.Equal(3, ticks.Count);
        Assert.Equal(6950.00, ticks[0].Price);
        Assert.Equal(6940.00, ticks[1].Price);
        Assert.Equal(6960.00, ticks[2].Price);
    }

    [Fact]
    public void Roundtrip_ByteIdentical()
    {
        var builder = TickSet.CreateBuilder(ES, Feb2, H26_ES);
        builder.Add(28800000, PriceKind.Bid, 6900.00m, 10);
        builder.Add(28800000, PriceKind.Ask, 6900.25m, 15);
        builder.Add(28800004, PriceKind.TradeAsk, 6900.25m, 3);
        var original = builder.Build();

        using var ms1 = new MemoryStream();
        StbaEncoder.Encode(original, ms1);
        var bytes1 = ms1.ToArray();

        var decoded = RoundTrip(original);
        using var ms2 = new MemoryStream();
        StbaEncoder.Encode(decoded, ms2);
        var bytes2 = ms2.ToArray();

        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void Add_OutOfOrder_Throws()
    {
        var builder = TickSet.CreateBuilder(ES, Feb2, H26_ES);
        builder.Add(28800004, PriceKind.TradeBid, 6900.00m, 1);

        Assert.Throws<InvalidOperationException>(() =>
            builder.Add(28800000, PriceKind.TradeBid, 6900.00m, 2));
    }

    [Fact]
    public void Add_OutOfOrder_SameTime_WrongKind_Throws()
    {
        var builder = TickSet.CreateBuilder(ES, Feb2, H26_ES);
        builder.Add(28800000, PriceKind.Ask, 6900.00m, 1);

        Assert.Throws<InvalidOperationException>(() =>
            builder.Add(28800000, PriceKind.Bid, 6900.00m, 2));
    }

    [Fact]
    public void PriceKind_TradeMask_MatchesBothTradeKinds()
    {
        Assert.True((PriceKind.TradeBid & PriceKind.Trade) != 0);
        Assert.True((PriceKind.TradeAsk & PriceKind.Trade) != 0);
        Assert.False((PriceKind.Bid & PriceKind.Trade) != 0);
        Assert.False((PriceKind.Ask & PriceKind.Trade) != 0);
    }

    [Fact]
    public void Decode_BadMagic_Throws()
    {
        var data = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x04 };
        using var ms = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => StbaDecoder.Decode(ms));
    }

    [Fact]
    public void Decode_WrongVersion_Throws()
    {
        var data = "STBA"u8.ToArray().Concat(new byte[] { 0x03 }).ToArray();
        using var ms = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => StbaDecoder.Decode(ms));
    }

    [Fact]
    public void Encode_InvalidPriceKind_Throws()
    {
        var builder = TickSet.CreateBuilder(ES, Feb2, H26_ES);
        builder.Add(28800000, (PriceKind)0, 23600, 1);   // 0 = uninitialized / invalid
        var ts = builder.Build();

        using var ms = new MemoryStream();
        Assert.Throws<InvalidOperationException>(() => StbaEncoder.Encode(ts, ms));
    }

    private static TickSet RoundTrip(TickSet original)
    {
        using var encoded = new MemoryStream();
        StbaEncoder.Encode(original, encoded);
        encoded.Position = 0;
        return StbaDecoder.Decode(encoded);
    }
}
