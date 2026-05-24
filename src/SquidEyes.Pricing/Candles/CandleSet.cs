namespace SquidEyes.Pricing;

/// <summary>
/// Rolling collection of <see cref="Candle"/>s built tick-by-tick. Newest candle lives at
/// index <c>0</c>; older candles slide down the list as new ones are added. Once <c>Count</c>
/// reaches <see cref="Capacity"/>, the oldest entry is evicted on each new arrival.
///
/// Subclasses define how ticks roll into bars:
///   <list type="bullet">
///     <item><see cref="IntervalCandleSet"/> — time-based (every N seconds).</item>
///     <item><see cref="RenkoCandleSet"/>    — price-based (every N ticks of movement).</item>
///   </list>
///
/// The semantic of <see cref="Current"/> depends on the concrete kind:
///   <list type="bullet">
///     <item>Interval: the in-progress, still-mutating bar.</item>
///     <item>Renko:   the most recently closed brick (Renko bricks are born closed).</item>
///   </list>
///
/// Not thread-safe. Trade-only — non-trade ticks (Bid, Ask) are silently ignored.
/// </summary>
public abstract class CandleSet
{
    private readonly List<Candle> _candles = new();

    /// <summary>Maximum number of candles retained. Oldest is evicted when full.</summary>
    public int Capacity { get; }

    /// <summary>Raised each time a candle transitions from open to closed.</summary>
    public event EventHandler<CandleClosedEventArgs>? CandleClosed;

    protected CandleSet(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        Capacity = capacity;
    }

    /// <summary>Number of candles currently retained (0..<see cref="Capacity"/>).</summary>
    public int Count => _candles.Count;

    /// <summary>
    /// Newest candle, or <c>null</c> when the set is empty. For interval sets this is the
    /// in-progress bar; for Renko sets this is the most recently closed brick.
    /// </summary>
    public Candle? Current => _candles.Count > 0 ? _candles[0] : null;

    /// <summary>
    /// Indexer by "bars ago": <c>0</c> is the newest, <c>1</c> the one before, etc.
    /// Throws <see cref="ArgumentOutOfRangeException"/> for out-of-range indices.
    /// </summary>
    public Candle this[int barsAgo]
    {
        get
        {
            if (barsAgo < 0 || barsAgo >= _candles.Count)
                throw new ArgumentOutOfRangeException(nameof(barsAgo));

            return _candles[barsAgo];
        }
    }

    /// <summary>Snapshot of all retained candles, newest first.</summary>
    public IReadOnlyList<Candle> AsReadOnly() => _candles.AsReadOnly();

    /// <summary>
    /// Feed a tick into the set. Non-trade ticks (Bid, Ask) are ignored. The concrete
    /// subclass decides whether this tick updates the current candle, closes it and
    /// starts a new one, or generates one-or-more new closed candles (Renko).
    /// </summary>
    public abstract void ProcessTick(Tick tick);

    /// <summary>Push a new candle to the front (index 0), evicting the tail if at capacity.</summary>
    protected void PushFront(Candle candle)
    {
        _candles.Insert(0, candle);

        if (_candles.Count > Capacity)
            _candles.RemoveAt(_candles.Count - 1);
    }

    /// <summary>Raise <see cref="CandleClosed"/> for <paramref name="closed"/>.</summary>
    protected void RaiseClosed(Candle closed, DateTime triggerET) =>
        CandleClosed?.Invoke(this, new CandleClosedEventArgs(closed, triggerET));
}
