namespace SquidEyes.Pricing;

public static partial class DateOnlyExtenders
{
    public static bool IsWeekday(this DateOnly value)
    {
        return value.DayOfWeek >= DayOfWeek.Monday
            && value.DayOfWeek <= DayOfWeek.Friday;
    }

    public static string Format(this DateOnly value) =>
        value.ToString("MM/dd/yyyy");

    public static bool IsTradeDate(this DateOnly date)
    {
        return date >= Session.MinDate
            && date <= Session.MaxDate
            && date.IsWeekday()
            && !date.IsHoliday()
            && !date.IsEarlyCloseDay()
            && !date.IsReducedLiquidityDay();
    }

    /// <summary>
    /// The earliest supported trade date — the first <see cref="IsTradeDate"/> on or after
    /// <see cref="Session.MinDate"/>.
    /// </summary>
    public static DateOnly EarliestTradeDate()
    {
        var d = Session.MinDate;
        while (d <= Session.MaxDate && !d.IsTradeDate())
            d = d.AddDays(1);
        return d;
    }

    /// <summary>
    /// The latest trade date strictly before <paramref name="date"/>, clamped to the supported
    /// window. Returns the same date if a trade date itself is supplied for "yesterday".
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No trade date exists strictly before <paramref name="date"/> within
    /// <see cref="Session.MinDate"/>..<see cref="Session.MaxDate"/>.
    /// </exception>
    public static DateOnly LatestTradeDateBefore(this DateOnly date)
    {
        var d = date.AddDays(-1);
        if (d > Session.MaxDate)
            d = Session.MaxDate;
        while (d >= Session.MinDate && !d.IsTradeDate())
            d = d.AddDays(-1);
        if (d < Session.MinDate)
            throw new InvalidOperationException(
                $"No trade date exists strictly before {date:yyyy-MM-dd} " +
                $"within [{Session.MinDate:yyyy-MM-dd}..{Session.MaxDate:yyyy-MM-dd}].");
        return d;
    }

    /// <summary>
    /// Enumerates every trade date in the inclusive range <c>[from, until]</c>, in ascending
    /// order. Non-trade dates (weekends, holidays, out-of-window) are silently skipped.
    /// </summary>
    public static IEnumerable<DateOnly> EnumerateTradeDates(DateOnly from, DateOnly until)
    {
        for (var d = from; d <= until; d = d.AddDays(1))
            if (d.IsTradeDate())
                yield return d;
    }

    private static bool IsHoliday(this DateOnly date)
    {
        return date.IsNewYearsDay()
            || date.IsChristmas()
            || date.IsGoodFriday()
            || date.IsIndependenceDay()
            || date.IsThanksgivingDay();
    }

    private static bool IsEarlyCloseDay(this DateOnly date)
    {
        return date.IsMartinLutherKingDay()
            || date.IsPresidentsDay()
            || date.IsMemorialDay()
            || date.IsJuneteenth()
            || date.IsLaborDay()
            || date.IsBlackFriday()
            || date.IsChristmasEve()
            || date.IsNewYearsEve();
    }

    private static bool IsReducedLiquidityDay(this DateOnly date) =>
        date.IsEasterMonday() || date.IsBoxingDay();
}
