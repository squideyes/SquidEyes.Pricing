using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.MiscHelpers;

public class EasternTimeTests
{
    [Fact]
    public void Zone_ResolvesToEastern()
    {
        // Either id works depending on the OS; both refer to the same zone.
        Assert.Contains(EasternTime.Zone.Id, new[] { "America/New_York", "Eastern Standard Time" });
    }

    [Fact]
    public void FromUtc_DuringEST_IsMinusFiveHours()
    {
        // 2026-01-15 15:00 UTC -> 2026-01-15 10:00 ET (EST, UTC-5)
        var utc = new DateTimeOffset(2026, 1, 15, 15, 0, 0, TimeSpan.Zero);
        var et = EasternTime.FromUtc(utc);
        Assert.Equal(new DateTime(2026, 1, 15, 10, 0, 0), et);
    }

    [Fact]
    public void FromUtc_DuringEDT_IsMinusFourHours()
    {
        // 2026-07-15 15:00 UTC -> 2026-07-15 11:00 ET (EDT, UTC-4)
        var utc = new DateTimeOffset(2026, 7, 15, 15, 0, 0, TimeSpan.Zero);
        var et = EasternTime.FromUtc(utc);
        Assert.Equal(new DateTime(2026, 7, 15, 11, 0, 0), et);
    }

    [Fact]
    public void ToUtc_DuringEST_AddsFiveHours()
    {
        // 2026-01-15 10:00 ET (EST) -> 15:00 UTC
        var et = new DateTime(2026, 1, 15, 10, 0, 0);
        var utc = EasternTime.ToUtc(et);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 15, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void ToUtc_DuringEDT_AddsFourHours()
    {
        // 2026-07-15 11:00 ET (EDT) -> 15:00 UTC
        var et = new DateTime(2026, 7, 15, 11, 0, 0);
        var utc = EasternTime.ToUtc(et);
        Assert.Equal(new DateTimeOffset(2026, 7, 15, 15, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void ToUtc_RoundTripsFromUtc()
    {
        var original = new DateTimeOffset(2026, 3, 17, 14, 30, 0, TimeSpan.Zero);
        var et = EasternTime.FromUtc(original);
        var back = EasternTime.ToUtc(et);
        Assert.Equal(original, back);
    }

    [Fact]
    public void ToUtc_IgnoresInputKind()
    {
        // ToUtc should treat the input as ET wall-clock regardless of Kind.
        var asLocal = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Local);
        var asUtc = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var asUnspec = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal(EasternTime.ToUtc(asUnspec), EasternTime.ToUtc(asLocal));
        Assert.Equal(EasternTime.ToUtc(asUnspec), EasternTime.ToUtc(asUtc));
    }

    [Fact]
    public void TodayEt_ReturnsADate()
    {
        // We can't assert a specific value, but TodayEt should always equal the date
        // component of FromUtc(now).
        var now = DateTimeOffset.UtcNow;
        var expected = DateOnly.FromDateTime(EasternTime.FromUtc(now));
        var actual = EasternTime.TodayEt();
        // Allow a 1-day slack in case the test crosses midnight ET between calls.
        Assert.InRange((actual.DayNumber - expected.DayNumber), -1, 1);
    }

    [Fact]
    public void WindowToUtc_ReturnsBothEndpointsInUtc()
    {
        var date = new DateOnly(2026, 1, 15);
        var (start, end) = EasternTime.WindowToUtc(date, new TimeOnly(8, 0), new TimeOnly(16, 0));

        // EST in mid-January = UTC-5
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 13, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 21, 0, 0, TimeSpan.Zero), end);
    }

    [Fact]
    public void WindowToUtc_HandlesDstCorrectly()
    {
        // March 8, 2026 is a Sunday; DST begins that day at 02:00 ET. A weekday window after
        // that should be EDT (UTC-4).
        var date = new DateOnly(2026, 3, 9);   // Monday after DST switch
        var (start, _) = EasternTime.WindowToUtc(date, new TimeOnly(8, 0), new TimeOnly(16, 0));
        Assert.Equal(new DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero), start);
    }
}
