using SquidEyes.Pricing.Stbad;
using static SquidEyes.Pricing.UnitTests.Stbad.StbadTestData;

namespace SquidEyes.Pricing.UnitTests.Stbad;

public class StbadCsvAndQualityTests
{
    [Fact]
    public void Csv_EmitsHeaderAndRowsPerSlotAndTrade()
    {
        var b = NewBuilder();
        b.AddQuote(Base, [
            new SlotChange(BookSide.Bid, 0, 1000, 10, 1),
            new SlotChange(BookSide.Ask, 0, 1002, 12, 2),
        ]);
        b.AddTrade(Base.AddMilliseconds(1), PriceKind.TradeAsk, 1002, 3);

        using var sw = new StringWriter();
        StbadCsvEncoder.Encode(b.Build(), sw);
        var lines = sw.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(StbadCsvEncoder.Header, lines[0]);
        Assert.Contains(",B,0,250,10,1", lines[1]); // 1000 ticks * 0.25 = 250.0
        Assert.Contains(",A,0,250.5,12,2", lines[2]);
        Assert.StartsWith($"{Base.AddMilliseconds(1):yyyy-MM-ddTHH:mm:ss.fffffff},L,,250.5,3,", lines[3]);
    }

    [Fact]
    public void Quality_ReportsCountsFillAndBytesPerEvent()
    {
        var opt = new StbadOptions { KeyframeMaxEvents = 8, KeyframeMaxMillis = 1_000_000 };
        var b = NewBuilder();
        AddFullBook(b, Base, 1000, 1002);
        for (var i = 1; i <= 30; i++)
        {
            var t = Base.AddMilliseconds(i);
            if (i % 6 == 0)
                b.AddTrade(t, PriceKind.TradeBid, 1000, 2);
            else
                b.AddQuote(t, [new SlotChange(BookSide.Bid, 0, 1000, 100 + i, 1)]);
        }

        using var ms = new MemoryStream();
        StbadEncoder.Encode(b.Build(), ms, opt);
        ms.Position = 0;

        var report = DepthQualityReport.Analyze(ms);

        Assert.Equal(31, report.TotalEvents);        // 1 full book + 30
        Assert.Equal(5, report.TradeEvents);          // i = 6,12,18,24,30
        Assert.Equal(26, report.QuoteEvents);
        Assert.True(report.Keyframes >= 1);
        Assert.True(report.BytesPerEvent > 0);
        Assert.Equal(1.0, report.BidFillRate[0], 3);  // inside bid always occupied
        Assert.True(report.CrossedFraction is >= 0 and <= 1);
        Assert.False(string.IsNullOrWhiteSpace(report.ToString()));
    }
}
