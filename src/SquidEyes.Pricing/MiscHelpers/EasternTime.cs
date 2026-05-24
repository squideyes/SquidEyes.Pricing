namespace SquidEyes.Pricing;

/// <summary>
/// Shared Eastern Time helpers. The library deals exclusively in ET (see
/// <see cref="Tick.OnET"/>, <see cref="Candle.FromET"/>, <see cref="Session.From"/> /
/// <see cref="Session.Until"/>), so consumers that need to convert to/from UTC or
/// ask "what's today's ET trade date?" share a single resolver here.
/// </summary>
/// <remarks>
/// <para>Resolves the IANA id <c>America/New_York</c> first (works on .NET 8+ on every
/// supported OS) and falls back to the Windows id <c>Eastern Standard Time</c>. DST is
/// handled by <see cref="TimeZoneInfo"/>.</para>
/// </remarks>
public static class EasternTime
{
    /// <summary>The resolved Eastern Time zone (handles DST).</summary>
    public static readonly TimeZoneInfo Zone = ResolveZone();

    /// <summary>Converts a UTC instant to its ET wall-clock <see cref="DateTime"/>.</summary>
    public static DateTime FromUtc(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, Zone).DateTime;

    /// <summary>
    /// Converts an ET wall-clock <see cref="DateTime"/> to a UTC <see cref="DateTimeOffset"/>.
    /// The input is treated as Eastern wall-clock regardless of its <see cref="DateTime.Kind"/>.
    /// </summary>
    public static DateTimeOffset ToUtc(DateTime etWallClock)
    {
        var unspecified = DateTime.SpecifyKind(etWallClock, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, Zone.GetUtcOffset(unspecified)).ToUniversalTime();
    }

    /// <summary>The current calendar date in Eastern Time.</summary>
    public static DateOnly TodayEt() =>
        DateOnly.FromDateTime(FromUtc(DateTimeOffset.UtcNow));

    /// <summary>
    /// Returns the half-open UTC window <c>[StartUtc, EndUtc)</c> corresponding to the ET
    /// wall-clock window <c>[date+startEt, date+endEt)</c>. Useful for building requests to
    /// upstream APIs that take UTC bounds.
    /// </summary>
    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) WindowToUtc(
        DateOnly date, TimeOnly startEt, TimeOnly endEt) =>
            (ToUtc(date.ToDateTime(startEt)), ToUtc(date.ToDateTime(endEt)));

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { /* try the next id */ }
            catch (InvalidTimeZoneException) { /* try the next id */ }
        }
        throw new InvalidOperationException(
            "Could not resolve the Eastern Time zone " +
            "(tried 'America/New_York' and 'Eastern Standard Time').");
    }
}
