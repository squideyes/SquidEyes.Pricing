namespace SquidEyes.Pricing;

public sealed class Instrument
{
    private static readonly Instrument[] Cache = BuildCache();

    private Instrument(Symbol symbol, InstrumentKind kind, decimal tickSize, double pointValue)
    {
        Symbol = symbol;
        Kind = kind;
        TickSize = tickSize;
        PointValue = pointValue;
        TicksPerPoint = (int)(1.0m / tickSize);
    }

    public Symbol Symbol { get; }
    public InstrumentKind Kind { get; }
    public decimal TickSize { get; }
    public int TicksPerPoint { get; }
    public double PointValue { get; }

    public double Round(double price)
    {
        double ts = (double)TickSize;
        return Math.Round(price / ts) * ts;
    }

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
        var specs = new Dictionary<Symbol, (InstrumentKind Kind, decimal TickSize, double PointValue)>
        {
            [Symbol.ES] = (InstrumentKind.Future, 0.25m,       50.0),
            [Symbol.NQ] = (InstrumentKind.Future, 0.25m,       20.0),
            [Symbol.CL] = (InstrumentKind.Future, 0.01m,       1000.0),
            [Symbol.GC] = (InstrumentKind.Future, 0.10m,       100.0),
            [Symbol.TY] = (InstrumentKind.Future, 0.015625m,   1000.0),
            [Symbol.FV] = (InstrumentKind.Future, 0.0078125m,  1000.0),
            [Symbol.US] = (InstrumentKind.Future, 0.03125m,    1000.0),
            [Symbol.JY] = (InstrumentKind.Future, 0.0000005m,  125000.0),
            [Symbol.EU] = (InstrumentKind.Future, 0.00005m,    125000.0),
            [Symbol.BP] = (InstrumentKind.Future, 0.0001m,     62500.0),
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
