using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Sessions;

public class DateOnlyExtendersTests
{
    [Theory]
    [InlineData(2026, 1, 5, true)]
    [InlineData(2026, 1, 9, true)]
    [InlineData(2026, 1, 10, false)]
    [InlineData(2026, 1, 11, false)]
    [InlineData(2026, 1, 7, true)]
    public void IsWeekday_ReturnsCorrectResult(int y, int m, int d, bool expected)
    {
        Assert.Equal(expected, new DateOnly(y, m, d).IsWeekday());
    }

    [Fact]
    public void Format_ReturnsCorrectFormat()
    {
        var date = new DateOnly(2026, 3, 15);
        Assert.Equal("03/15/2026", date.Format());
    }

    [Theory]
    [InlineData(2026, 1, 2, true)]
    [InlineData(2026, 1, 3, false)]
    [InlineData(2026, 1, 4, false)]
    [InlineData(2026, 1, 1, false)]
    [InlineData(2026, 12, 25, false)]
    [InlineData(2026, 1, 19, false)]
    [InlineData(2026, 7, 3, false)]
    [InlineData(2024, 1, 2, true)]
    [InlineData(2023, 12, 29, false)]
    [InlineData(2028, 12, 22, true)]
    [InlineData(2028, 12, 25, false)]
    public void IsTradeDate_ReturnsCorrectResult(int y, int m, int d, bool expected)
    {
        Assert.Equal(expected, new DateOnly(y, m, d).IsTradeDate());
    }

    [Fact]
    public void IsTradeDate_GoodFriday2026_IsFalse()
    {
        Assert.False(new DateOnly(2026, 4, 3).IsTradeDate());
    }

    [Fact]
    public void IsTradeDate_EasterMonday2026_IsFalse()
    {
        Assert.False(new DateOnly(2026, 4, 6).IsTradeDate());
    }

    [Fact]
    public void IsTradeDate_ThanksgivingDay2026_IsFalse()
    {
        Assert.False(new DateOnly(2026, 11, 26).IsTradeDate());
    }

    [Fact]
    public void IsTradeDate_BlackFriday2026_IsFalse()
    {
        Assert.False(new DateOnly(2026, 11, 27).IsTradeDate());
    }

    [Fact]
    public void IsTradeDate_MemorialDay2026_IsFalse()
    {
        Assert.False(new DateOnly(2026, 5, 25).IsTradeDate());
    }

    [Fact]
    public void IsTradeDate_LaborDay2026_IsFalse()
    {
        Assert.False(new DateOnly(2026, 9, 7).IsTradeDate());
    }

    [Fact]
    public void IsTradeDate_PresidentsDay2026_IsFalse()
    {
        Assert.False(new DateOnly(2026, 2, 16).IsTradeDate());
    }

    [Fact]
    public void IsTradeDate_Juneteenth2026_IsFalse()
    {
        Assert.False(new DateOnly(2026, 6, 19).IsTradeDate());
    }

    [Fact]
    public void IsTradeDate_ChristmasEve2026_IsFalse()
    {
        Assert.False(new DateOnly(2026, 12, 24).IsTradeDate());
    }

    [Fact]
    public void IsTradeDate_NewYearsEve2026_IsFalse()
    {
        Assert.False(new DateOnly(2026, 12, 31).IsTradeDate());
    }

    [Fact]
    public void IsTradeDate_BoxingDay2025_IsFalse()
    {
        Assert.False(new DateOnly(2025, 12, 26).IsTradeDate());
    }

    [Fact]
    public void IsTradeDate_RegularWeekday_IsTrue()
    {
        Assert.True(new DateOnly(2026, 3, 4).IsTradeDate());
    }

    [Fact]
    public void EarliestTradeDate_IsFirstTradeDateOnOrAfterMinDate()
    {
        var earliest = DateOnlyExtenders.EarliestTradeDate();

        Assert.True(earliest.IsTradeDate());
        Assert.True(earliest >= Session.MinDate);
        Assert.True(earliest.AddDays(-1) < Session.MinDate
                    || !earliest.AddDays(-1).IsTradeDate());
    }

    [Fact]
    public void EarliestTradeDate_MatchesKnownValue()
    {
        // 2024-01-01 is New Year's Day; Session.MinDate is 2024-01-02 (Tuesday). The first
        // actual trade date on or after MinDate is 2024-01-02 itself.
        Assert.Equal(new DateOnly(2024, 1, 2), DateOnlyExtenders.EarliestTradeDate());
    }

    [Fact]
    public void LatestTradeDateBefore_TuesdayAfterTradeDate_IsPriorMonday()
    {
        // 2026-03-04 Wed (trade date). The latest trade date before it is 2026-03-03 Tue.
        Assert.Equal(new DateOnly(2026, 3, 3),
            new DateOnly(2026, 3, 4).LatestTradeDateBefore());
    }

    [Fact]
    public void LatestTradeDateBefore_Monday_SkipsWeekend()
    {
        // Monday 2026-03-09 — latest trade date before it is Friday 2026-03-06.
        Assert.Equal(new DateOnly(2026, 3, 6),
            new DateOnly(2026, 3, 9).LatestTradeDateBefore());
    }

    [Fact]
    public void LatestTradeDateBefore_AfterHoliday_SkipsHoliday()
    {
        // 2026-07-04 falls on a Saturday, observed Friday 2026-07-03 (Independence Day),
        // so the latest trade date before 2026-07-06 (Mon) is 2026-07-02 (Thu).
        Assert.Equal(new DateOnly(2026, 7, 2),
            new DateOnly(2026, 7, 6).LatestTradeDateBefore());
    }

    [Fact]
    public void LatestTradeDateBefore_ClampsToMaxDate()
    {
        // A date past MaxDate clamps down to the latest trade date within [MinDate..MaxDate].
        var farFuture = new DateOnly(2099, 1, 1);
        var latest = farFuture.LatestTradeDateBefore();

        Assert.True(latest.IsTradeDate());
        Assert.True(latest <= Session.MaxDate);
    }

    [Fact]
    public void LatestTradeDateBefore_ImpossiblyEarlyDate_Throws()
    {
        var beforeMin = Session.MinDate;
        Assert.Throws<InvalidOperationException>(() => beforeMin.LatestTradeDateBefore());
    }

    [Fact]
    public void EnumerateTradeDates_SkipsWeekendsAndHolidays()
    {
        // Spanning Christmas week: 12/24 (CE), 12/25 (Christmas), 12/26 (Sat), 12/27 (Sun),
        // and 12/31 (NYE) are all non-trade.
        var dates = DateOnlyExtenders
            .EnumerateTradeDates(new DateOnly(2026, 12, 21), new DateOnly(2026, 12, 31))
            .ToList();

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 12, 21),
                new DateOnly(2026, 12, 22),
                new DateOnly(2026, 12, 23),
                new DateOnly(2026, 12, 28),
                new DateOnly(2026, 12, 29),
                new DateOnly(2026, 12, 30),
            },
            dates);
    }

    [Fact]
    public void EnumerateTradeDates_InclusiveBothEnds()
    {
        // 2026-01-02 Friday (trade date) through 2026-01-06 Tuesday (trade date). Both
        // endpoints are trade dates and the weekend in between is skipped.
        var dates = DateOnlyExtenders
            .EnumerateTradeDates(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 6))
            .ToList();

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 1, 2),
                new DateOnly(2026, 1, 5),
                new DateOnly(2026, 1, 6),
            },
            dates);
    }

    [Fact]
    public void EnumerateTradeDates_EmptyWhenFromAfterUntil()
    {
        var dates = DateOnlyExtenders
            .EnumerateTradeDates(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 5))
            .ToList();

        Assert.Empty(dates);
    }
}
