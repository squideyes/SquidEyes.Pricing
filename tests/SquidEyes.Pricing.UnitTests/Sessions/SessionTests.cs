using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Sessions;

public class SessionTests
{
    private static readonly DateOnly Jan2_2024 = new(2024, 1, 2);

    [Fact]
    public void Create_DTH_SetsProperties()
    {
        var session = Session.Create(Jan2_2024, SessionKind.DTH);

        Assert.Equal(Jan2_2024, session.Date);
        Assert.Equal(SessionKind.DTH, session.Kind);
        Assert.Equal(new DateTime(2024, 1, 2, 8, 0, 0),  session.From);
        Assert.Equal(new DateTime(2024, 1, 2, 16, 0, 0), session.Until);
        Assert.Empty(session.Embargoes);
    }

    [Fact]
    public void Create_RTH_SetsProperties()
    {
        var session = Session.Create(Jan2_2024, SessionKind.RTH);

        Assert.Equal(new DateTime(2024, 1, 2, 9, 30, 0), session.From);
        Assert.Equal(new DateTime(2024, 1, 2, 16, 0, 0), session.Until);
    }

    [Fact]
    public void Create_InvalidTradeDate_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Session.Create(new DateOnly(2024, 1, 6), SessionKind.DTH));

    [Fact]
    public void Create_Holiday_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Session.Create(new DateOnly(2026, 1, 1), SessionKind.DTH));

    [Fact]
    public void Contains_WithinRange_True()
    {
        var s = Session.Create(Jan2_2024, SessionKind.DTH);
        Assert.True(s.Contains(new DateTime(2024, 1, 2, 10, 0, 0)));
    }

    [Fact]
    public void Contains_AtFrom_True()
    {
        var s = Session.Create(Jan2_2024, SessionKind.DTH);
        Assert.True(s.Contains(new DateTime(2024, 1, 2, 8, 0, 0)));
    }

    [Fact]
    public void Contains_AtUntil_False()
    {
        var s = Session.Create(Jan2_2024, SessionKind.DTH);
        Assert.False(s.Contains(new DateTime(2024, 1, 2, 16, 0, 0)));
    }

    [Fact]
    public void Contains_BeforeFrom_False()
    {
        var s = Session.Create(Jan2_2024, SessionKind.DTH);
        Assert.False(s.Contains(new DateTime(2024, 1, 2, 7, 59, 59)));
    }

    [Fact]
    public void Contains_NonUnspecifiedKind_Throws()
    {
        var s = Session.Create(Jan2_2024, SessionKind.DTH);
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Contains(DateTime.UtcNow));
    }

    [Fact]
    public void IsEmbargoed_NoEmbargoes_False()
    {
        var s = Session.Create(Jan2_2024, SessionKind.DTH);
        Assert.False(s.IsEmbargoed(new DateTime(2024, 1, 2, 10, 0, 0)));
    }

    [Fact]
    public void IsTradable_WindowAndNotEmbargoed_True()
    {
        var s = Session.Create(Jan2_2024, SessionKind.DTH);
        Assert.True(s.IsTradable(new DateTime(2024, 1, 2, 10, 0, 0)));
    }

    [Fact]
    public void IsTradable_OutsideWindow_False()
    {
        var s = Session.Create(Jan2_2024, SessionKind.DTH);
        Assert.False(s.IsTradable(new DateTime(2024, 1, 2, 7, 0, 0)));
    }

    [Fact]
    public void IsTradable_InsideEmbargo_False()
    {
        var s = Session.Create(Jan2_2024, SessionKind.DTH);
        s.AddAdHocEmbargo(new(10, 0), new(10, 5), "blackout");

        Assert.False(s.IsTradable(new DateTime(2024, 1, 2, 10, 2, 0)));
        Assert.True (s.IsTradable(new DateTime(2024, 1, 2, 10, 6, 0)));
    }

    [Fact]
    public void AddAdHocEmbargo_Appends_AndReturnsCreated()
    {
        var s = Session.Create(Jan2_2024, SessionKind.DTH);
        var e = s.AddAdHocEmbargo(new(10, 0), new(10, 5), "blackout");

        Assert.Single(s.Embargoes);
        Assert.Same(e, s.Embargoes[0]);
        Assert.Equal(EmbargoKind.AdHoc, e.Kind);
    }

    [Fact]
    public void AddNewsEmbargo_Appends_AndReturnsCreated()
    {
        var s = Session.Create(Jan2_2024, SessionKind.DTH);
        var e = s.AddNewsEmbargo(new DateTime(2024, 1, 2, 14, 30, 0), NewsImpact.High, "FOMC");

        Assert.Single(s.Embargoes);
        Assert.Equal(EmbargoKind.News, e.Kind);
    }

    [Fact]
    public void AddAnchoredEmbargo_Appends_AndReturnsCreated()
    {
        var s = Session.Create(Jan2_2024, SessionKind.DTH);
        var e = s.AddAnchoredEmbargo(SessionAnchor.Start, TimeSpan.FromMinutes(1), "open-1");

        Assert.Single(s.Embargoes);
        Assert.Equal(EmbargoKind.Anchored, e.Kind);
    }
}
