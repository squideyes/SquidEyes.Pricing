namespace SquidEyes.Pricing;

/// <summary>
/// A depth-aware tick set: the ordered <see cref="DepthEvent"/> stream for one
/// <c>(Instrument, Contract, Date, Session)</c> plus the materialized <see cref="DepthBook"/> at the
/// current cursor. The analog of <see cref="TickSet"/> for the <c>.stbad</c> depth format.
/// </summary>
public sealed class DepthTickSet
{
    // One row per event. Quote events index a contiguous run in <see cref="_changes"/>; trade
    // events carry their fields inline. Kept in source order (strictly non-decreasing TimeNs).
    internal readonly struct StoredEvent
    {
        public readonly long TimeNs;
        public readonly DepthEventType Type;
        public readonly PriceKind Aggressor;   // trade only
        public readonly int TradePriceTicks;   // trade only
        public readonly int TradeSize;         // trade only
        public readonly int ChangeOffset;      // quote only — start index into _changes
        public readonly int ChangeCount;       // quote only

        private StoredEvent(long timeNs, DepthEventType type, PriceKind aggressor,
            int tradePriceTicks, int tradeSize, int changeOffset, int changeCount)
        {
            TimeNs = timeNs;
            Type = type;
            Aggressor = aggressor;
            TradePriceTicks = tradePriceTicks;
            TradeSize = tradeSize;
            ChangeOffset = changeOffset;
            ChangeCount = changeCount;
        }

        public static StoredEvent Trade(long timeNs, PriceKind aggressor, int priceTicks, int size) =>
            new(timeNs, DepthEventType.Trade, aggressor, priceTicks, size, 0, 0);

        public static StoredEvent Quote(long timeNs, int changeOffset, int changeCount) =>
            new(timeNs, DepthEventType.Quote, default, 0, 0, changeOffset, changeCount);
    }

    private readonly List<StoredEvent> _events;
    private readonly List<SlotChange> _changes;

    public Instrument Instrument { get; }
    public DateOnly Date { get; }
    public Contract Contract { get; }
    public SessionKind Session { get; }
    public Source Source { get; }

    /// <summary>The ET wall-clock instant of the session start (the timestamp epoch).</summary>
    public DateTime SessionStart { get; }

    public int Count => _events.Count;

    private DepthTickSet(Instrument instrument, DateOnly date, Contract contract,
        SessionKind session, Source source, List<StoredEvent> events, List<SlotChange> changes)
    {
        Instrument = instrument;
        Date = date;
        Contract = contract;
        Session = session;
        Source = source;
        _events = events;
        _changes = changes;
        SessionStart = date.ToDateTime(session.ToTimes().From);
    }

    internal IReadOnlyList<StoredEvent> Events => _events;
    internal IReadOnlyList<SlotChange> Changes => _changes;

    /// <summary>
    /// Replays the stream, yielding each <see cref="DepthEvent"/> alongside the single reused
    /// <see cref="DepthBook"/> (already updated for that event). Quote events mutate the book; trade
    /// events leave it unchanged. Allocation-free per event (the book and tuple are reused/value types).
    /// </summary>
    public IEnumerable<(DepthEvent Event, DepthBook Book)> Replay()
    {
        var book = new DepthBook((double)Instrument.TickSize);
        var tickSize = (double)Instrument.TickSize;

        foreach (var e in _events)
        {
            var onET = SessionStart.AddTicks(e.TimeNs / 100);

            if (e.Type == DepthEventType.Trade)
            {
                yield return (
                    DepthEvent.Trade(onET, e.Aggressor, e.TradePriceTicks * tickSize, e.TradeSize),
                    book);
            }
            else
            {
                int bidMask = 0, askMask = 0;
                for (var i = 0; i < e.ChangeCount; i++)
                {
                    var c = _changes[e.ChangeOffset + i];
                    book.Apply(in c);
                    if (c.Side == BookSide.Bid) bidMask |= 1 << c.Level;
                    else askMask |= 1 << c.Level;
                }
                book.AssertInvariants();
                yield return (DepthEvent.Quote(onET, bidMask, askMask), book);
            }
        }
    }

    public static Builder CreateBuilder(
        Instrument instrument, DateOnly date, Contract contract, SessionKind session, Source source) =>
            new(instrument, date, contract, session, source);

    /// <summary>
    /// Accumulates depth events in source order. Timestamps must be strictly non-decreasing;
    /// same-timestamp events preserve insertion order (stable tie-break).
    /// </summary>
    public sealed class Builder
    {
        private readonly Instrument _instrument;
        private readonly DateOnly _date;
        private readonly Contract _contract;
        private readonly SessionKind _session;
        private readonly Source _source;
        private readonly DateTime _sessionStart;
        private readonly List<StoredEvent> _events = [];
        private readonly List<SlotChange> _changes = [];
        private long _lastTimeNs = -1;

        internal Builder(Instrument instrument, DateOnly date, Contract contract,
            SessionKind session, Source source)
        {
            _instrument = instrument;
            _date = date;
            _contract = contract;
            _session = session;
            _source = source;
            _sessionStart = date.ToDateTime(session.ToTimes().From);
        }

        private long ToNanos(DateTime onET)
        {
            var ns = (onET - _sessionStart).Ticks * 100L;
            if (ns < 0)
                throw new ArgumentOutOfRangeException(nameof(onET),
                    $"Event time {onET:O} precedes session start {_sessionStart:O}.");
            if (ns < _lastTimeNs)
                throw new InvalidOperationException(
                    $"Events must be added in non-decreasing time order. " +
                    $"Previous: {_lastTimeNs} ns, current: {ns} ns ({onET:O}).");
            _lastTimeNs = ns;
            return ns;
        }

        /// <summary>Appends a trade print (does not mutate the book).</summary>
        public void AddTrade(DateTime onET, PriceKind aggressor, int priceTicks, int size)
        {
            if (aggressor is not (PriceKind.TradeBid or PriceKind.TradeAsk))
                throw new ArgumentException(
                    $"Trade aggressor must be TradeBid or TradeAsk, got {aggressor}.", nameof(aggressor));

            _events.Add(StoredEvent.Trade(ToNanos(onET), aggressor, priceTicks, size));
        }

        /// <summary>
        /// Appends a quote update naming the changed slots. Empty (no-op) updates are ignored.
        /// </summary>
        public void AddQuote(DateTime onET, ReadOnlySpan<SlotChange> changes)
        {
            if (changes.IsEmpty)
                return;

            var timeNs = ToNanos(onET);
            var offset = _changes.Count;
            foreach (var c in changes)
            {
                if (c.Level < 0 || c.Level >= DepthBook.MaxLevels)
                    throw new ArgumentOutOfRangeException(nameof(changes),
                        $"Slot level {c.Level} out of range [0, {DepthBook.MaxLevels}).");
                _changes.Add(c);
            }
            _events.Add(StoredEvent.Quote(timeNs, offset, changes.Length));
        }

        public DepthTickSet Build() =>
            new(_instrument, _date, _contract, _session, _source, _events, _changes);
    }
}
