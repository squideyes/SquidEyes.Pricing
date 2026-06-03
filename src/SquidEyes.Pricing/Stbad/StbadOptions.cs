namespace SquidEyes.Pricing.Stbad;

/// <summary>
/// Per-file encoding knobs for <c>.stbad</c>. All four are written into the header so a file is
/// self-describing and choices are per-file, not compile-time (size-vs-fidelity levers: drop order
/// count, sparser keyframes, codec, timestamp resolution).
/// </summary>
public sealed record StbadOptions
{
    /// <summary>
    /// Store per-level order count (Databento <c>bid_ct</c>/<c>ask_ct</c>). Default on — it is the
    /// microstructure signal that justifies keeping depth; flip off if size becomes painful.
    /// </summary>
    public bool IncludeOrderCount { get; init; } = true;

    /// <summary>Compression codec for each block. Default Brotli.</summary>
    public byte Codec { get; init; } = StbadFormat.CodecBrotli;

    /// <summary>Start a new block/keyframe after at most this many delta events.</summary>
    public int KeyframeMaxEvents { get; init; } = StbadFormat.DefaultKeyframeMaxEvents;

    /// <summary>Start a new block/keyframe after at most this much wall time (ms).</summary>
    public int KeyframeMaxMillis { get; init; } = StbadFormat.DefaultKeyframeMaxMillis;

    public static StbadOptions Default { get; } = new();
}
