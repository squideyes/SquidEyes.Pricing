namespace SquidEyes.Pricing;

/// <summary>
/// Global defaults for how a News-kind <see cref="Embargo"/> widens around its
/// anchor time, based on the impact of the news item. Settable so backtests/algos
/// can tune them once at startup. Units are minutes.
/// </summary>
public static class NewsImpactDefaults
{
    public static PrePost High   { get; set; } = new(5, 15);
    public static PrePost Medium { get; set; } = new(3, 10);
    public static PrePost Low    { get; set; } = new(1,  2);

    internal static PrePost For(NewsImpact impact) => impact switch
    {
        NewsImpact.High   => High,
        NewsImpact.Medium => Medium,
        NewsImpact.Low    => Low,
        _ => throw new ArgumentOutOfRangeException(nameof(impact), $"Unknown NewsImpact: {impact}")
    };
}
