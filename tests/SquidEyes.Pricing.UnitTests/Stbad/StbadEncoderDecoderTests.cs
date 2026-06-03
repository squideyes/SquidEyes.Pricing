using SquidEyes.Pricing.Stbad;
using static SquidEyes.Pricing.UnitTests.Stbad.StbadTestData;

namespace SquidEyes.Pricing.UnitTests.Stbad;

public class StbadEncoderDecoderTests
{
    [Fact]
    public void Roundtrip_Empty_PreservesMetadata()
    {
        var ts = NewBuilder().Build();
        var decoded = RoundTrip(ts);

        Assert.Equal(Symbol.ES, decoded.Instrument.Symbol);
        Assert.Equal(Date, decoded.Date);
        Assert.Equal("H26", decoded.Contract.Code);
        Assert.Equal(SessionKind.MTH, decoded.Session);
        Assert.Equal(Source.DataBento, decoded.Source);
        Assert.Equal(0, decoded.Count);
    }

    [Fact]
    public void BytePinned_KeyframePlusSingleSlotQuote()
    {
        var b = NewBuilder();
        b.AddQuote(Base, [new SlotChange(BookSide.Bid, 0, 100, 5, 2)]);
        var file = Encode(b.Build(), new StbadOptions { Codec = StbadFormat.CodecNone });

        var payload = FirstBlockPayload(file);

        // 62-byte empty keyframe (tag 0, ts 0, then 60 zero ladder bytes) + 6-byte Q1.
        var expected = new byte[68];
        // keyframe is all zeros already; Q1 starts at offset 62:
        expected[62] = 0x01; // tag=Quote1 | side Bid(0) | level 0
        expected[63] = 0x00; // Δt = 0
        expected[64] = 0xC8; // Δprice = +100 -> zigzag 200 -> 0xC8 0x01
        expected[65] = 0x01;
        expected[66] = 0x05; // size = 5
        expected[67] = 0x04; // Δcount = +2 -> zigzag 4

        Assert.Equal(expected, payload);
    }

    [Fact]
    public void BytePinned_KeyframePlusSingleTrade()
    {
        var b = NewBuilder();
        b.AddTrade(Base, PriceKind.TradeBid, 100, 5);
        var file = Encode(b.Build(), new StbadOptions { Codec = StbadFormat.CodecNone });

        var payload = FirstBlockPayload(file);

        var expected = new byte[67];
        expected[62] = 0x03; // tag = TradeHit
        expected[63] = 0x00; // Δt = 0
        expected[64] = 0xC8; // Δprice = +100 from anchor 0 -> zigzag 200
        expected[65] = 0x01;
        expected[66] = 0x05; // size = 5

        Assert.Equal(expected, payload);
    }

    [Fact]
    public void Roundtrip_MixedEvents_PreservesBookAtEveryEvent()
    {
        var b = NewBuilder();
        AddFullBook(b, Base, bidTop: 1000, askBottom: 1002);
        b.AddQuote(Base.AddMilliseconds(1), [new SlotChange(BookSide.Bid, 0, 1000, 150, 3)]); // size tick
        b.AddTrade(Base.AddMilliseconds(2), PriceKind.TradeAsk, 1002, 4);                     // lift
        // re-rank: inside bid lifts a tick — top two bid slots change at once (multi-slot QN)
        var rerank = new SlotChange[2]
        {
            new(BookSide.Bid, 0, 1001, 50, 1),
            new(BookSide.Bid, 1, 1000, 150, 3),
        };
        b.AddQuote(Base.AddMilliseconds(3), rerank);
        b.AddQuote(Base.AddMilliseconds(4), [new SlotChange(BookSide.Ask, 9, 0, 0, 0)]); // empty a level
        b.AddTrade(Base.AddMilliseconds(5), PriceKind.TradeBid, 1000, 1);

        var original = b.Build();
        var decoded = RoundTrip(original);

        Assert.Equal(original.Count, decoded.Count);

        var oIt = original.Replay().GetEnumerator();
        var dIt = decoded.Replay().GetEnumerator();
        while (oIt.MoveNext())
        {
            Assert.True(dIt.MoveNext());
            Assert.Equal(oIt.Current.Event, dIt.Current.Event);
            Assert.Equal(Snapshot(oIt.Current.Book), Snapshot(dIt.Current.Book));
        }
        Assert.False(dIt.MoveNext());
    }

    [Fact]
    public void Roundtrip_ByteIdentical_AcrossKeyframeBoundaries()
    {
        // Tiny cadence forces several blocks/keyframes; encode(decode(bytes)) must reproduce bytes.
        var opt = new StbadOptions { KeyframeMaxEvents = 4, KeyframeMaxMillis = 1_000_000 };
        var b = NewBuilder();
        AddFullBook(b, Base, 1000, 1002);
        for (var i = 1; i <= 40; i++)
        {
            var t = Base.AddMilliseconds(i);
            if (i % 5 == 0)
                b.AddTrade(t, i % 2 == 0 ? PriceKind.TradeBid : PriceKind.TradeAsk, 1000 + (i % 3), 1 + i % 4);
            else
                b.AddQuote(t, [new SlotChange(BookSide.Bid, i % 10, 1000 - (i % 10), 100 + i, 1 + (i % 7))]);
        }
        var original = b.Build();

        var bytes1 = Encode(original, opt);
        using var ms = new MemoryStream(bytes1);
        var decoded = StbadDecoder.Decode(ms);
        var bytes2 = Encode(decoded, opt);

        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void Roundtrip_NoOrderCount_Variant()
    {
        var opt = new StbadOptions { IncludeOrderCount = false };
        var b = NewBuilder();
        AddFullBook(b, Base, 1000, 1002);
        b.AddQuote(Base.AddMilliseconds(1), [new SlotChange(BookSide.Bid, 0, 1000, 250, 9)]);
        var original = b.Build();

        var decoded = RoundTrip(original, opt);

        // Order count is dropped, so it decodes as 0; price/size survive.
        var (_, book) = decoded.Replay().Last();
        Assert.Equal(250, book.Bid(0).Size);
        Assert.Equal(0, book.Bid(0).OrderCount);
    }

    [Fact]
    public void Decode_CorruptBody_Throws()
    {
        var b = NewBuilder();
        AddFullBook(b, Base, 1000, 1002);
        var bytes = Encode(b.Build());
        bytes[^8] ^= 0xFF; // flip a byte inside the footer/body region

        using var ms = new MemoryStream(bytes);
        Assert.ThrowsAny<InvalidDataException>(() => StbadDecoder.Decode(ms));
    }

    [Fact]
    public void Decode_BadMagic_Throws()
    {
        using var ms = new MemoryStream(new byte[] { 0, 0, 0, 0, 0, 0, 1 });
        Assert.Throws<InvalidDataException>(() => StbadDecoder.Decode(ms));
    }

    [Fact]
    public void Book_CrossedAndLocked_AreFlaggedNotThrown()
    {
        var crossed = NewBuilder();
        crossed.AddQuote(Base, [
            new SlotChange(BookSide.Bid, 0, 1005, 10, 1),
            new SlotChange(BookSide.Ask, 0, 1003, 12, 1),
        ]);
        var (_, cb) = RoundTrip(crossed.Build()).Replay().Last();
        Assert.True(cb.IsCrossed);

        var locked = NewBuilder();
        locked.AddQuote(Base, [
            new SlotChange(BookSide.Bid, 0, 1003, 10, 1),
            new SlotChange(BookSide.Ask, 0, 1003, 12, 1),
        ]);
        var (_, lb) = RoundTrip(locked.Build()).Replay().Last();
        Assert.True(lb.IsLocked);
    }

    [Fact]
    public void Builder_OutOfOrderTime_Throws()
    {
        var b = NewBuilder();
        b.AddTrade(Base.AddMilliseconds(5), PriceKind.TradeBid, 1000, 1);
        Assert.Throws<InvalidOperationException>(() =>
            b.AddTrade(Base.AddMilliseconds(1), PriceKind.TradeBid, 1000, 1));
    }
}
