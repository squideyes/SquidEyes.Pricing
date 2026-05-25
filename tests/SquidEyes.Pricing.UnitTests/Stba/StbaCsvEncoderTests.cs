using System.Text;
using SquidEyes.Pricing;
using SquidEyes.Pricing.Stba;

namespace SquidEyes.Pricing.UnitTests.Stba;

public class StbaCsvEncoderTests
{
    private static readonly Instrument ES = Symbol.ES;
    private static readonly DateOnly Feb2 = new(2026, 2, 2);
    private static readonly Contract H26_ES = Contract.Create(Symbol.ES, "H26");

    [Fact]
    public void Encode_EmptyTickSet_WritesHeaderOnly()
    {
        var ts = TickSet.CreateBuilder(ES, Feb2, H26_ES).Build();

        var csv = EncodeToString(ts);

        Assert.Equal("OnET,Kind,Price,Size" + Environment.NewLine, csv);
    }

    [Fact]
    public void Encode_PreservesRowOrder()
    {
        var b = TickSet.CreateBuilder(ES, Feb2, H26_ES);
        b.Add(28800000, PriceKind.Bid,      6900.00, 10);
        b.Add(28800000, PriceKind.Ask,      6900.25, 15);
        b.Add(28800000, PriceKind.TradeAsk, 6900.25,  3);
        b.Add(28800100, PriceKind.TradeBid, 6900.00,  7);
        var ts = b.Build();

        var lines = EncodeToLines(ts);

        Assert.Equal("OnET,Kind,Price,Size", lines[0]);
        Assert.Equal("2026-02-02T08:00:00.000,B,6900,10",      lines[1]);
        Assert.Equal("2026-02-02T08:00:00.000,A,6900.25,15",   lines[2]);
        Assert.Equal("2026-02-02T08:00:00.000,L,6900.25,3",    lines[3]);
        Assert.Equal("2026-02-02T08:00:00.100,H,6900,7",       lines[4]);
    }

    [Theory]
    [InlineData(PriceKind.Bid,      'B')]
    [InlineData(PriceKind.Ask,      'A')]
    [InlineData(PriceKind.TradeBid, 'H')]
    [InlineData(PriceKind.TradeAsk, 'L')]
    public void KindCode_MapsEachPrimitiveKind(PriceKind kind, char expected)
    {
        Assert.Equal(expected, StbaCsvEncoder.KindCode(kind));
    }

    [Fact]
    public void KindCode_RejectsTradeMask()
    {
        Assert.Throws<InvalidOperationException>(() => StbaCsvEncoder.KindCode(PriceKind.Trade));
    }

    [Fact]
    public void KindCode_RejectsDefault()
    {
        Assert.Throws<InvalidOperationException>(() => StbaCsvEncoder.KindCode((PriceKind)0));
    }

    [Fact]
    public void Encode_StripsTrailingZerosFromPrice()
    {
        var b = TickSet.CreateBuilder(Symbol.NQ, Feb2, Contract.Create(Symbol.NQ, "H26"));
        b.Add(28800000, PriceKind.TradeAsk, 20000.50, 2);
        var ts = b.Build();

        var line = EncodeToLines(ts)[1];

        Assert.Contains(",20000.5,", line);
    }

    [Fact]
    public void Encode_RoundTripsThroughStreamOverload()
    {
        var b = TickSet.CreateBuilder(ES, Feb2, H26_ES);
        b.Add(28800000, PriceKind.Bid, 6900.00, 10);
        var ts = b.Build();

        using var ms = new MemoryStream();
        StbaCsvEncoder.Encode(ts, ms);
        ms.Position = 0;
        var bytes = ms.ToArray();

        // No UTF-8 BOM should be emitted.
        Assert.NotEqual((byte)0xEF, bytes[0]);

        var text = Encoding.UTF8.GetString(bytes);
        Assert.StartsWith("OnET,Kind,Price,Size", text);
        Assert.Contains(",B,6900,10", text);
    }

    [Fact]
    public void Encode_NullTickSet_Throws()
    {
        using var sw = new StringWriter();
        Assert.Throws<ArgumentNullException>(() => StbaCsvEncoder.Encode(null!, sw));
    }

    [Fact]
    public void Encode_NullTextWriter_Throws()
    {
        var ts = TickSet.CreateBuilder(ES, Feb2, H26_ES).Build();
        Assert.Throws<ArgumentNullException>(() => StbaCsvEncoder.Encode(ts, (TextWriter)null!));
    }

    [Fact]
    public void Encode_NullStream_Throws()
    {
        var ts = TickSet.CreateBuilder(ES, Feb2, H26_ES).Build();
        Assert.Throws<ArgumentNullException>(() => StbaCsvEncoder.Encode(ts, (Stream)null!));
    }

    private static string EncodeToString(TickSet ts)
    {
        using var sw = new StringWriter();
        StbaCsvEncoder.Encode(ts, sw);
        return sw.ToString();
    }

    private static string[] EncodeToLines(TickSet ts) =>
        EncodeToString(ts).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
