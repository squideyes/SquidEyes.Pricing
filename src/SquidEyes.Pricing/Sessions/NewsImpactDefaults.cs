namespace SquidEyes.Pricing;

/// <summary>
/// Global defaults for how a News-kind <see cref="Embargo"/> widens around its
/// anchor time, based on the impact of the news item. Settable so backtests/algos can
/// tune them once at startup instead of per-embargo.
/// </summary>
public static class NewsImpactDefaults
{
    public static int HighBeforeMinutes   { get; set; } = 5;
    public static int HighAfterMinutes    { get; set; } = 15;
    public static int MediumBeforeMinutes { get; set; } = 3;
    public static int MediumAfterMinutes  { get; set; } = 10;

    internal static (int Before, int After) For(NewsImpact impact) => impact switch
    {
        NewsImpact.High   => (HighBeforeMinutes,   HighAfterMinutes),
        NewsImpact.Medium => (MediumBeforeMinutes, MediumAfterMinutes),
        _ => throw new ArgumentOutOfRangeException(nameof(impact), $"Unknown NewsImpact: {impact}")
    };
}
