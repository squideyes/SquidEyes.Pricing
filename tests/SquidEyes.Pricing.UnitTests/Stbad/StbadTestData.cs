using SquidEyes.Pricing.Stbad;

namespace SquidEyes.Pricing.UnitTests.Stbad;

/// <summary>Shared fixtures + helpers for the <c>.stbad</c> tests.</summary>
internal static class StbadTestData
{
    public static readonly Instrument ES = Symbol.ES;
    public static readonly DateOnly Date = new(2026, 2, 2);
    public static readonly Contract H26 = Contract.Create(Symbol.ES, "H26");
    public static readonly DateTime Base = Date.ToDateTime(new TimeOnly(8, 0)); // ET session start

    public static DepthTickSet.Builder NewBuilder() =>
        DepthTickSet.CreateBuilder(ES, Date, H26, SessionKind.MTH, Source.DataBento);

    /// <summary>Adds a full, valid 10-level book on both sides (bids descending, asks ascending).</summary>
    public static void AddFullBook(DepthTickSet.Builder b, DateTime t, int bidTop, int askBottom)
    {
        var ch = new SlotChange[2 * DepthBook.MaxLevels];
        for (var i = 0; i < DepthBook.MaxLevels; i++)
            ch[i] = new SlotChange(BookSide.Bid, i, bidTop - i, 100 + i, 1 + i);
        for (var i = 0; i < DepthBook.MaxLevels; i++)
            ch[DepthBook.MaxLevels + i] = new SlotChange(BookSide.Ask, i, askBottom + i, 200 + i, 2 + i);
        b.AddQuote(t, ch);
    }

    /// <summary>Encode → decode round trip via the eager decoder.</summary>
    public static DepthTickSet RoundTrip(DepthTickSet ts, StbadOptions? opt = null)
    {
        using var ms = new MemoryStream();
        StbadEncoder.Encode(ts, ms, opt ?? StbadOptions.Default);
        ms.Position = 0;
        return StbadDecoder.Decode(ms);
    }

    public static byte[] Encode(DepthTickSet ts, StbadOptions? opt = null)
    {
        using var ms = new MemoryStream();
        StbadEncoder.Encode(ts, ms, opt ?? StbadOptions.Default);
        return ms.ToArray();
    }

    /// <summary>Snapshots both ladders (bids then asks) for value comparison.</summary>
    public static DepthLevel[] Snapshot(DepthBook book)
    {
        var snap = new DepthLevel[2 * DepthBook.MaxLevels];
        for (var i = 0; i < DepthBook.MaxLevels; i++)
        {
            snap[i] = book.Bid(i);
            snap[DepthBook.MaxLevels + i] = book.Ask(i);
        }
        return snap;
    }

    /// <summary>Returns the raw payload of the first block (valid only for CodecNone files).</summary>
    public static byte[] FirstBlockPayload(byte[] file)
    {
        using var ms = new MemoryStream(file);
        using var r = new BinaryReader(ms);
        StbadHeader.Read(r);                  // leaves position at end of header
        var len = (int)r.ReadUInt32();        // compressed length (== raw under CodecNone)
        return r.ReadBytes(len);
    }
}
