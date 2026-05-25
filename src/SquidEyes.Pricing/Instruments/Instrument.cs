namespace SquidEyes.Pricing;

public sealed class Instrument
{
    private static readonly Instrument[] Cache = BuildCache();

    private Instrument(Symbol symbol, InstrumentKind kind, double tickSize, double pointValue)
    {
        Symbol = symbol;
        Kind = kind;
        TickSize = tickSize;
        PointValue = pointValue;
        TicksPerPoint = (int)Math.Round(1.0 / tickSize);
    }

    public Symbol Symbol { get; }
    public InstrumentKind Kind { get; }
    public double TickSize { get; }
    public int TicksPerPoint { get; }
    public double PointValue { get; }

    public double Round(double price) =>
        Math.Round(price / TickSize) * TickSize;

    public static Instrument Create(Symbol symbol) => Cache[(int)symbol];

    public static implicit operator Instrument(Symbol symbol) => Create(symbol);

    public static Instrument Parse(string code)
    {
        if (!Enum.TryParse<Symbol>(code, true, out var symbol))
            throw new ArgumentException($"Unsupported symbol: {code}", nameof(code));

        return Create(symbol);
    }

    public static bool IsSupported(string code) =>
        Enum.TryParse<Symbol>(code, true, out _);

    public override string ToString() => Symbol.ToString();

    private static Instrument[] BuildCache()
    {
        var specs = new Dictionary<Symbol, (InstrumentKind Kind, double TickSize, double PointValue)>
        {
            [Symbol.ES]  = (InstrumentKind.Future, 0.25,       50.0),
            [Symbol.NQ]  = (InstrumentKind.Future, 0.25,       20.0),
            [Symbol.CL]  = (InstrumentKind.Future, 0.01,       1000.0),
            [Symbol.GC]  = (InstrumentKind.Future, 0.10,       100.0),
            [Symbol.TY]  = (InstrumentKind.Future, 0.015625,   1000.0),
            [Symbol.FV]  = (InstrumentKind.Future, 0.0078125,  1000.0),
            [Symbol.US]  = (InstrumentKind.Future, 0.03125,    1000.0),
            [Symbol.JY]  = (InstrumentKind.Future, 0.0000005,  125000.0),
            [Symbol.EU]  = (InstrumentKind.Future, 0.00005,    125000.0),
            [Symbol.BP]  = (InstrumentKind.Future, 0.0001,     62500.0),
            // E-micro futures — 1/10 the notional of their big siblings.
            [Symbol.MES] = (InstrumentKind.Future, 0.25,       5.0),
            [Symbol.MNQ] = (InstrumentKind.Future, 0.25,       2.0),
            [Symbol.RTY] = (InstrumentKind.Future, 0.10,       50.0),
            [Symbol.M2K] = (InstrumentKind.Future, 0.10,       5.0),
            [Symbol.MGC] = (InstrumentKind.Future, 0.10,       10.0),
            [Symbol.MCL] = (InstrumentKind.Future, 0.01,       100.0),
        };

        var values = Enum.GetValues<Symbol>();
        var max = (int)values.Max();
        var cache = new Instrument[max + 1];

        foreach (var s in values)
        {
            var (kind, tickSize, pointValue) = specs[s];
            cache[(int)s] = new Instrument(s, kind, tickSize, pointValue);
        }

        return cache;
    }
}
