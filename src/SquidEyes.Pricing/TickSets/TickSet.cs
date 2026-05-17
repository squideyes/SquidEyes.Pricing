using System.Collections;

namespace SquidEyes.Pricing;

public sealed class TickSet : IEnumerable<Tick>
{
    private readonly List<TickData> ticks;

    public Instrument Instrument { get; }
    public DateOnly Date { get; }
    public Contract Contract { get; }
    public int Count => ticks.Count;

    private TickSet(Instrument instrument, DateOnly date, Contract contract, List<TickData> ticks)
    {
        Instrument = instrument;
        Date = date;
        Contract = contract;
        this.ticks = ticks;
    }

    public IEnumerator<Tick> GetEnumerator()
    {
        var baseDate = Date.ToDateTime(TimeOnly.MinValue);
        var tickSize = (double)Instrument.TickSize;

        foreach (var td in ticks)
        {
            yield return new Tick(
                baseDate.AddMilliseconds(td.TimeMs),
                td.Kind,
                td.PriceTicks * tickSize,
                td.Size);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal IReadOnlyList<TickData> RawTicks => ticks;

    public static Builder CreateBuilder(Instrument instrument, DateOnly date, Contract contract) =>
        new(instrument, date, contract);

    public sealed class Builder
    {
        private readonly Instrument instrument;
        private readonly DateOnly date;
        private readonly Contract contract;
        private readonly List<TickData> ticks = [];

        internal Builder(Instrument instrument, DateOnly date, Contract contract)
        {
            this.instrument = instrument;
            this.date = date;
            this.contract = contract;
        }

        public void Add(int timeMs, PriceKind kind, int priceTicks, int size)
        {
            var tick = new TickData(timeMs, kind, priceTicks, size);

            if (ticks.Count > 0)
            {
                var last = ticks[^1];
                var cmp = last.CompareTo(tick);

                if (cmp == 0)
                {
                    ticks[^1] = new TickData(last.TimeMs, last.Kind, last.PriceTicks, last.Size + size);
                    return;
                }

                if (cmp > 0)
                    throw new InvalidOperationException(
                        $"Ticks must be added in order. Previous: ({last.TimeMs}, {last.Kind}, {last.PriceTicks}), " +
                        $"Current: ({timeMs}, {kind}, {priceTicks})");
            }

            ticks.Add(tick);
        }

        public void Add(int timeMs, PriceKind kind, decimal price, int size)
        {
            var priceTicks = (int)Math.Round(price / instrument.TickSize);
            Add(timeMs, kind, priceTicks, size);
        }

        public TickSet Build() => new(instrument, date, contract, ticks);
    }
}
