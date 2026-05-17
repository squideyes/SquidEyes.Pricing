namespace SquidEyes.Pricing;

/// <summary>
/// A named trading window pinned to a specific trade-date in Eastern Time. The window is
/// determined by <see cref="Kind"/> (e.g. RTH = 09:30-16:00). Optional <see cref="Embargo"/>
/// instances can be attached to mark sub-windows where activity is suspended.
/// </summary>
public sealed class Session
{
    public static readonly DateOnly MinDate = new(2024, 1, 2);
    public static readonly DateOnly MaxDate = new(2028, 12, 22);

    private readonly List<Embargo> embargoes = [];

    public DateOnly    Date  { get; }
    public SessionKind Kind  { get; }
    public DateTime    From  { get; }
    public DateTime    Until { get; }

    public IReadOnlyList<Embargo> Embargoes => embargoes;

    private Session(DateOnly date, SessionKind kind, DateTime from, DateTime until)
    {
        Date = date;
        Kind = kind;
        From = from;
        Until = until;
    }

    public static Session Create(DateOnly date, SessionKind kind)
    {
        if (!date.IsTradeDate())
            throw new ArgumentOutOfRangeException(nameof(date), $"\"{date}\" is an invalid trade-date.");

        var (from, until) = kind.ToTimes();

        return new Session(date, kind, date.ToDateTime(from), date.ToDateTime(until));
    }

    /// <summary>True if <paramref name="when"/> falls within the session window. Half-open: [From, Until).</summary>
    public bool Contains(DateTime when)
    {
        if (when.Kind != DateTimeKind.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(when), "\"when.Kind\" must be \"Unspecified\".");

        return when >= From && when < Until;
    }

    /// <summary>True if any attached <see cref="Embargo"/> covers <paramref name="when"/>.</summary>
    public bool IsEmbargoed(DateTime when)
    {
        foreach (var embargo in embargoes)
        {
            if (embargo.IsEmbargoed(this, when))
                return true;
        }

        return false;
    }

    /// <summary>True iff <see cref="Contains"/> is true AND <see cref="IsEmbargoed"/> is false.</summary>
    public bool IsTradable(DateTime when) => Contains(when) && !IsEmbargoed(when);

    /// <summary>Adds an <see cref="EmbargoKind.AdHoc"/> embargo with explicit ET times.</summary>
    public Embargo AddAdHocEmbargo(TimeOnly from, TimeOnly until, string reason)
    {
        var embargo = Embargo.AdHoc(from, until, reason);
        embargoes.Add(embargo);

        return embargo;
    }

    /// <summary>
    /// Adds a <see cref="EmbargoKind.News"/> embargo anchored to a wall-clock DateTime.
    /// The window widens by <see cref="NewsImpactDefaults"/> minutes before/after based on
    /// <paramref name="impact"/>.
    /// </summary>
    public Embargo AddNewsEmbargo(DateTime at, NewsImpact impact, string reason)
    {
        var embargo = Embargo.News(at, impact, reason);
        embargoes.Add(embargo);

        return embargo;
    }

    /// <summary>
    /// Adds an <see cref="EmbargoKind.Anchored"/> embargo anchored to this session's start or
    /// end, lasting <paramref name="duration"/>.
    /// </summary>
    public Embargo AddAnchoredEmbargo(SessionAnchor anchor, TimeSpan duration, string reason)
    {
        var embargo = Embargo.Anchored(anchor, duration, reason);
        embargoes.Add(embargo);

        return embargo;
    }
}
