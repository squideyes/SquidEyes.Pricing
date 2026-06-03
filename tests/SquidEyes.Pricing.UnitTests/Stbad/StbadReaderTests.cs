using SquidEyes.Pricing.Stbad;
using static SquidEyes.Pricing.UnitTests.Stbad.StbadTestData;

namespace SquidEyes.Pricing.UnitTests.Stbad;

public class StbadReaderTests
{
    private static DepthTickSet BuildStream(int events)
    {
        var b = NewBuilder();
        AddFullBook(b, Base, 1000, 1002);
        for (var i = 1; i <= events; i++)
        {
            var t = Base.AddMilliseconds(i);
            if (i % 7 == 0)
                b.AddTrade(t, i % 2 == 0 ? PriceKind.TradeBid : PriceKind.TradeAsk, 1000, 1 + (i % 5));
            else
                b.AddQuote(t, [new SlotChange(BookSide.Bid, i % 10, 1000 - (i % 10), 100 + i, 1 + (i % 6))]);
        }
        return b.Build();
    }

    [Fact]
    public void Reader_ForwardMatchesEagerReplay()
    {
        var opt = new StbadOptions { KeyframeMaxEvents = 8, KeyframeMaxMillis = 1_000_000 };
        var ts = BuildStream(50);
        var bytes = Encode(ts, opt);

        var eager = ts.Replay().Select(x => (x.Event, Snapshot(x.Book))).ToList();

        using var ms = new MemoryStream(bytes);
        using var reader = new StbadReader(ms);
        var i = 0;
        while (reader.Read(out var e))
        {
            Assert.Equal(eager[i].Event, e);
            Assert.Equal(eager[i].Item2, Snapshot(reader.Book));
            i++;
        }
        Assert.Equal(eager.Count, i);
    }

    [Fact]
    public void Seek_ReturnsCorrectBookAtTarget()
    {
        var opt = new StbadOptions { KeyframeMaxEvents = 8, KeyframeMaxMillis = 1_000_000 };
        var ts = BuildStream(60);
        var bytes = Encode(ts, opt);

        // Reference: first event with OnET >= target, and the book right after it.
        var target = Base.AddMilliseconds(33);
        DepthEvent expectedEvent = default;
        DepthLevel[]? expectedBook = null;
        foreach (var (e, book) in ts.Replay())
        {
            if (e.OnET >= target)
            {
                expectedEvent = e;
                expectedBook = Snapshot(book);
                break;
            }
        }
        Assert.NotNull(expectedBook);

        using var ms = new MemoryStream(bytes);
        using var reader = new StbadReader(ms);
        reader.Seek(target);
        Assert.True(reader.Read(out var got));
        Assert.Equal(expectedEvent, got);
        Assert.Equal(expectedBook, Snapshot(reader.Book));
    }

    [Fact]
    public void Seek_PastEnd_ReturnsNoEvents()
    {
        var ts = BuildStream(20);
        var bytes = Encode(ts);

        using var ms = new MemoryStream(bytes);
        using var reader = new StbadReader(ms);
        reader.Seek(Base.AddHours(10));
        Assert.False(reader.Read(out _));
    }

    [Fact]
    public void Forward_Decode_IsAllocationFreePerEvent()
    {
        // One big block so block-load allocations don't dominate the per-event measurement.
        var opt = new StbadOptions { KeyframeMaxEvents = int.MaxValue, KeyframeMaxMillis = int.MaxValue };
        var ts = BuildStream(2000);
        var bytes = Encode(ts, opt);

        using var ms = new MemoryStream(bytes);
        using var reader = new StbadReader(ms);

        // Warm up: load the first block + JIT the read path.
        for (var i = 0; i < 16; i++)
            Assert.True(reader.Read(out _));

        long sink = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        while (reader.Read(out var e))
            sink += e.Size + (int)e.Type + e.BidChangedMask;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated == 0, $"expected zero per-event allocation, saw {allocated} bytes");
        Assert.True(sink >= 0); // keep the loop from being optimized away
    }
}
