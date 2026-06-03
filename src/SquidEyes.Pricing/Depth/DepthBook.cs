using System.Diagnostics;

namespace SquidEyes.Pricing;

/// <summary>
/// The materialized top-10 order book on both sides. Held as parallel integer-tick ladders and
/// mutated in place by the decoder (one instance per stream, reused across every event) so forward
/// iteration is allocation-free. Real prices are reconstructed on demand as
/// <c>priceTicks * TickSize</c>.
/// </summary>
/// <remarks>
/// Slot 0 is the inside; bids are stored descending, asks ascending. An empty slot (book shorter
/// than 10 on that side) has size 0. The book follows the slot-overwrite model: each event names
/// the slots that changed and their new values; nothing is inserted, shifted, or sorted.
/// </remarks>
public sealed class DepthBook
{
    /// <summary>Levels kept per side.</summary>
    public const int MaxLevels = 10;

    // Parallel arrays (px in ticks) avoid struct copies and stay cache-friendly on the hot path.
    private readonly int[] _bidPx = new int[MaxLevels];
    private readonly int[] _bidSz = new int[MaxLevels];
    private readonly int[] _bidCt = new int[MaxLevels];
    private readonly int[] _askPx = new int[MaxLevels];
    private readonly int[] _askSz = new int[MaxLevels];
    private readonly int[] _askCt = new int[MaxLevels];

    private readonly double _tickSize;

    public DepthBook(double tickSize) => _tickSize = tickSize;

    /// <summary>Number of levels per side (always <see cref="MaxLevels"/>).</summary>
    public int Levels => MaxLevels;

    /// <summary>The instrument tick size used to reconstruct real prices.</summary>
    public double TickSize => _tickSize;

    /// <summary>The bid level at <paramref name="level"/> (0 = inside), priced on demand.</summary>
    public DepthLevel Bid(int level) =>
        new(_bidPx[level] * _tickSize, _bidSz[level], _bidCt[level]);

    /// <summary>The ask level at <paramref name="level"/> (0 = inside), priced on demand.</summary>
    public DepthLevel Ask(int level) =>
        new(_askPx[level] * _tickSize, _askSz[level], _askCt[level]);

    /// <summary>The inside bid (level 0).</summary>
    public DepthLevel BestBid => Bid(0);

    /// <summary>The inside ask (level 0).</summary>
    public DepthLevel BestAsk => Ask(0);

    /// <summary>L1 projection so simple consumers can ignore depth entirely.</summary>
    public (double Bid, int BidSize, double Ask, int AskSize) L1 =>
        (_bidPx[0] * _tickSize, _bidSz[0], _askPx[0] * _tickSize, _askSz[0]);

    /// <summary>
    /// True when the inside bid is at or above the inside ask (both occupied). Real feeds cross
    /// briefly; this is surfaced for callers to flag, never thrown.
    /// </summary>
    public bool IsCrossed => _bidSz[0] > 0 && _askSz[0] > 0 && _bidPx[0] > _askPx[0];

    /// <summary>True when the inside bid equals the inside ask (both occupied).</summary>
    public bool IsLocked => _bidSz[0] > 0 && _askSz[0] > 0 && _bidPx[0] == _askPx[0];

    // ---- internal raw tick accessors (used by the encoder/decoder, same assembly) ----

    internal int BidPxTicks(int level) => _bidPx[level];
    internal int BidSize(int level) => _bidSz[level];
    internal int BidCount(int level) => _bidCt[level];
    internal int AskPxTicks(int level) => _askPx[level];
    internal int AskSize(int level) => _askSz[level];
    internal int AskCount(int level) => _askCt[level];

    internal void SetBid(int level, int priceTicks, int size, int count)
    {
        // Empty slots normalize to (0,0,0) so price-delta math is symmetric on encode/decode.
        if (size == 0) { priceTicks = 0; count = 0; }
        _bidPx[level] = priceTicks;
        _bidSz[level] = size;
        _bidCt[level] = count;
    }

    internal void SetAsk(int level, int priceTicks, int size, int count)
    {
        if (size == 0) { priceTicks = 0; count = 0; }
        _askPx[level] = priceTicks;
        _askSz[level] = size;
        _askCt[level] = count;
    }

    internal void Apply(in SlotChange change)
    {
        if (change.Side == BookSide.Bid)
            SetBid(change.Level, change.PriceTicks, change.Size, change.OrderCount);
        else
            SetAsk(change.Level, change.PriceTicks, change.Size, change.OrderCount);
    }

    internal void ResetAll()
    {
        Array.Clear(_bidPx); Array.Clear(_bidSz); Array.Clear(_bidCt);
        Array.Clear(_askPx); Array.Clear(_askSz); Array.Clear(_askCt);
    }

    [Conditional("DEBUG")]
    internal void AssertInvariants()
    {
        for (var i = 1; i < MaxLevels; i++)
        {
            if (_bidSz[i] > 0 && _bidSz[i - 1] > 0)
                Debug.Assert(_bidPx[i] < _bidPx[i - 1], "bids must be strictly descending");
            if (_askSz[i] > 0 && _askSz[i - 1] > 0)
                Debug.Assert(_askPx[i] > _askPx[i - 1], "asks must be strictly ascending");
        }
        // A crossed book is flagged via IsCrossed, never asserted — real feeds cross briefly.
    }
}
