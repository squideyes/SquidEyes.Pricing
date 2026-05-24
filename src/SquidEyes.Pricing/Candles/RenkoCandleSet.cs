namespace SquidEyes.Pricing;

/// <summary>
/// Price-based <see cref="CandleSet"/>: a brick is emitted every time the trade price moves
/// <see cref="BrickTicks"/> ticks (i.e. <see cref="BrickSize"/> in price) away from the
/// previous brick's close. Bricks are born already closed — <see cref="CandleSet.Current"/>
/// refers to the most recently completed brick.
///
/// Two flavours:
///   <list type="bullet">
///     <item><c>withWicks=false</c> — OHLC is constrained to the brick body
///           (up brick: O=base, H=top, L=base, C=top).</item>
///     <item><c>withWicks=true</c>  — high/low extend to the actual price extremes seen
///           between the previous brick's close and this brick's close. For a chain of
///           bricks closed by a single tick, only the chain's first brick carries the
///           "from-direction" wick and only the last carries the "into-direction" wick;
///           intermediate bricks have no wicks (their bodies span the moved range).</item>
///   </list>
///
/// Anchoring: the first trade tick rounds to the nearest brick boundary; that anchor
/// becomes the implicit close of a "brick zero". Bricks fire once price moves a full
/// <see cref="BrickSize"/> away from there.
///
/// Window semantics: a brick's <see cref="Candle.FromET"/> is the previous brick's
/// <c>UntilET</c> (or the first-tick time for the first brick); <see cref="Candle.UntilET"/>
/// is the timestamp of the tick that closed the brick. For a chain of bricks closed by a
/// single tick, successive brick windows are advanced by one DateTime tick each so that
/// <see cref="Candle.FromET"/> &lt; <see cref="Candle.UntilET"/> always holds.
///
/// Volume / TickCount on Renko bricks are always zero — Renko is a price-driven
/// abstraction that doesn't naturally allocate volume to specific bricks.
///
/// Ticks must be fed in chronological order. Not thread-safe.
/// </summary>
public sealed class RenkoCandleSet : CandleSet
{
    private readonly decimal _brickSizeDec;
    private readonly double  _brickSize;
    private readonly bool    _withWicks;

    private decimal?  _lastCloseDec;
    private DateTime? _anchorET;
    private DateTime? _lastBrickEndET;
    private double    _runningHigh;
    private double    _runningLow;

    public RenkoCandleSet(Instrument instrument, int brickTicks, int capacity, bool withWicks)
        : base(capacity)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        if (brickTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(brickTicks), "BrickTicks must be positive.");

        Instrument    = instrument;
        BrickTicks    = brickTicks;
        _brickSizeDec = brickTicks * instrument.TickSize;
        _brickSize    = (double)_brickSizeDec;
        _withWicks    = withWicks;
    }

    public Instrument Instrument { get; }
    public int        BrickTicks { get; }
    public double     BrickSize  => _brickSize;
    public bool       WithWicks  => _withWicks;

    public override void ProcessTick(Tick tick)
    {
        if (tick.OnET.Kind != DateTimeKind.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(tick), "\"tick.OnET.Kind\" must be \"Unspecified\".");

        if ((tick.Kind & PriceKind.Trade) == 0)
            return;

        var price    = tick.Price;
        var now      = tick.OnET;
        var priceDec = (decimal)price;

        if (_lastCloseDec is null)
        {
            var anchorDec = Math.Round(priceDec / _brickSizeDec, MidpointRounding.ToEven) * _brickSizeDec;
            _lastCloseDec = anchorDec;
            _anchorET     = now;
            _runningHigh  = price;
            _runningLow   = price;
            return;
        }

        if (price > _runningHigh) _runningHigh = price;
        if (price < _runningLow)  _runningLow  = price;

        var upCount = 0;

        while (priceDec >= _lastCloseDec.Value + (upCount + 1) * _brickSizeDec)
            upCount++;

        if (upCount > 0)
        {
            EmitChain(direction: +1, count: upCount, triggerET: now);
            return;
        }

        var downCount = 0;

        while (priceDec <= _lastCloseDec.Value - (downCount + 1) * _brickSizeDec)
            downCount++;

        if (downCount > 0)
            EmitChain(direction: -1, count: downCount, triggerET: now);
    }

    private void EmitChain(int direction, int count, DateTime triggerET)
    {
        for (var i = 0; i < count; i++)
        {
            var openDec  = _lastCloseDec!.Value + direction * i * _brickSizeDec;
            var closeDec = openDec + direction * _brickSizeDec;
            var open     = (double)openDec;
            var close    = (double)closeDec;

            double high, low;

            if (_withWicks)
            {
                if (direction == +1)
                {
                    low  = (i == 0)         ? Math.Min(open,  _runningLow)  : open;
                    high = (i == count - 1) ? Math.Max(close, _runningHigh) : close;
                }
                else
                {
                    high = (i == 0)         ? Math.Max(open,  _runningHigh) : open;
                    low  = (i == count - 1) ? Math.Min(close, _runningLow)  : close;
                }
            }
            else
            {
                if (direction == +1) { low = open;  high = close; }
                else                 { low = close; high = open;  }
            }

            EmitBrick(open, high, low, close, triggerET);
        }

        _lastCloseDec = _lastCloseDec!.Value + direction * count * _brickSizeDec;

        var newClose = (double)_lastCloseDec.Value;

        _runningHigh = newClose;
        _runningLow  = newClose;
    }

    private void EmitBrick(double open, double high, double low, double close, DateTime triggerET)
    {
        var from  = _lastBrickEndET ?? _anchorET!.Value;
        var until = triggerET > from ? triggerET : from.AddTicks(1);

        if (_lastBrickEndET is not null && until <= _lastBrickEndET.Value)
            until = _lastBrickEndET.Value.AddTicks(1);

        var brick = new Candle(from, until, open, high, low, close, volume: 0, tickCount: 0);

        _lastBrickEndET = until;

        PushFront(brick);
        RaiseClosed(brick, triggerET);
    }
}
