namespace SquidEyes.Pricing;

/// <summary>
/// A no-trade window within an owning <see cref="Session"/>. Three flavours, distinguished
/// by <see cref="Kind"/>:
///   <list type="bullet">
///     <item><see cref="EmbargoKind.AdHoc"/>     — explicit (From, Until) in ET.</item>
///     <item><see cref="EmbargoKind.News"/>      — anchored to a DateTime; widens by
///                                                  <see cref="NewsImpactDefaults"/> based on
///                                                  <see cref="Impact"/>.</item>
///     <item><see cref="EmbargoKind.Anchored"/>  — relative to the owning session's start or
///                                                  end, for <see cref="Duration"/>.</item>
///   </list>
/// Create one via <see cref="Session.AddAdHocEmbargo"/>, <see cref="Session.AddNewsEmbargo"/>,
/// or <see cref="Session.AddAnchoredEmbargo"/>.
/// </summary>
public sealed class Embargo
{
    public EmbargoKind Kind { get; }
    public string Reason { get; }

    // Populated only when Kind == AdHoc
    public TimeOnly? AdHocFrom  { get; }
    public TimeOnly? AdHocUntil { get; }

    // Populated only when Kind == News
    public DateTime?   NewsAt { get; }
    public NewsImpact? Impact { get; }

    // Populated only when Kind == Anchored
    public SessionAnchor? Anchor   { get; }
    public TimeSpan?      Duration { get; }

    private Embargo(EmbargoKind kind, string reason,
        TimeOnly? adHocFrom = null, TimeOnly? adHocUntil = null,
        DateTime? newsAt = null,    NewsImpact? impact = null,
        SessionAnchor? anchor = null, TimeSpan? duration = null)
    {
        Kind = kind;
        Reason = reason;
        AdHocFrom = adHocFrom;
        AdHocUntil = adHocUntil;
        NewsAt = newsAt;
        Impact = impact;
        Anchor = anchor;
        Duration = duration;
    }

    internal static Embargo AdHoc(TimeOnly from, TimeOnly until, string reason)
    {
        if (string.IsNullOrEmpty(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        if (from >= until)
            throw new ArgumentException("\"from\" must be earlier than \"until\".", nameof(from));

        return new Embargo(EmbargoKind.AdHoc, reason, adHocFrom: from, adHocUntil: until);
    }

    internal static Embargo News(DateTime at, NewsImpact impact, string reason)
    {
        if (string.IsNullOrEmpty(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        return new Embargo(EmbargoKind.News, reason, newsAt: at, impact: impact);
    }

    internal static Embargo Anchored(SessionAnchor anchor, TimeSpan duration, string reason)
    {
        if (string.IsNullOrEmpty(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be positive.");

        return new Embargo(EmbargoKind.Anchored, reason, anchor: anchor, duration: duration);
    }

    /// <summary>
    /// True if <paramref name="when"/> falls inside this embargo's window, resolved against
    /// the owning <paramref name="session"/>. Half-open: [From, Until).
    /// </summary>
    public bool IsEmbargoed(Session session, DateTime when)
    {
        if (when.Kind != DateTimeKind.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(when), "\"when.Kind\" must be \"Unspecified\".");

        var (from, until) = ResolveRange(session);
        var tod = TimeOnly.FromDateTime(when);

        return tod >= from && tod < until;
    }

    /// <summary>
    /// Resolves this embargo to an absolute ET (From, Until) window for the given session.
    /// Useful for logging and visualization.
    /// </summary>
    public (TimeOnly From, TimeOnly Until) ResolveRange(Session session) => Kind switch
    {
        EmbargoKind.AdHoc    => (AdHocFrom!.Value, AdHocUntil!.Value),
        EmbargoKind.News     => ResolveNewsRange(),
        EmbargoKind.Anchored => ResolveAnchoredRange(session),
        _ => throw new InvalidOperationException($"Unknown EmbargoKind: {Kind}")
    };

    private (TimeOnly From, TimeOnly Until) ResolveNewsRange()
    {
        var (before, after) = NewsImpactDefaults.For(Impact!.Value);
        var fromDt  = NewsAt!.Value.AddMinutes(-before);
        var untilDt = NewsAt!.Value.AddMinutes(after);

        return (TimeOnly.FromDateTime(fromDt), TimeOnly.FromDateTime(untilDt));
    }

    private (TimeOnly From, TimeOnly Until) ResolveAnchoredRange(Session session)
    {
        if (Anchor == SessionAnchor.Start)
        {
            var from  = TimeOnly.FromDateTime(session.From);
            var until = TimeOnly.FromDateTime(session.From + Duration!.Value);

            return (from, until);
        }

        var endFrom  = TimeOnly.FromDateTime(session.Until - Duration!.Value);
        var endUntil = TimeOnly.FromDateTime(session.Until);

        return (endFrom, endUntil);
    }
}
