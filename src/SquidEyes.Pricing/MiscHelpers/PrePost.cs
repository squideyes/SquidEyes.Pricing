namespace SquidEyes.Pricing;

/// <summary>
/// A symmetric "buffer around an anchor" pair — how much room sits before the anchor
/// and how much sits after. Units are intentionally caller-defined (minutes for
/// <see cref="NewsImpactDefaults"/>; could be seconds, bars, ticks, etc. for other uses).
/// </summary>
public readonly record struct PrePost(int Pre, int Post);
