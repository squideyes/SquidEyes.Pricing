using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Sessions;

public class EmbargoTests
{
    private static readonly DateOnly Jan2_2024 = new(2024, 1, 2);

    private static Session NewDth() => Session.Create(Jan2_2024, SessionKind.DTH);

    // ── AdHoc ──────────────────────────────────────────────────────────

    [Fact]
    public void AdHoc_ResolveRange_ReturnsExplicitTimes()
    {
        var s = NewDth();
        var e = s.AddAdHocEmbargo(new(10, 0), new(10, 5), "blackout");
        var (from, until) = e.ResolveRange(s);

        Assert.Equal(new TimeOnly(10, 0), from);
        Assert.Equal(new TimeOnly(10, 5), until);
    }

    [Fact]
    public void AdHoc_IsEmbargoed_InsideWindow_True()
    {
        var s = NewDth();
        var e = s.AddAdHocEmbargo(new(10, 0), new(10, 5), "blackout");
        Assert.True(e.IsEmbargoed(s, new DateTime(2024, 1, 2, 10, 2, 0)));
    }

    [Fact]
    public void AdHoc_IsEmbargoed_AtFrom_True()
    {
        var s = NewDth();
        var e = s.AddAdHocEmbargo(new(10, 0), new(10, 5), "blackout");
        Assert.True(e.IsEmbargoed(s, new DateTime(2024, 1, 2, 10, 0, 0)));
    }

    [Fact]
    public void AdHoc_IsEmbargoed_AtUntil_False()
    {
        var s = NewDth();
        var e = s.AddAdHocEmbargo(new(10, 0), new(10, 5), "blackout");
        Assert.False(e.IsEmbargoed(s, new DateTime(2024, 1, 2, 10, 5, 0)));
    }

    [Fact]
    public void AdHoc_FromAfterUntil_Throws()
    {
        var s = NewDth();
        Assert.Throws<ArgumentException>(
            () => s.AddAdHocEmbargo(new(10, 5), new(10, 0), "bad"));
    }

    [Fact]
    public void AdHoc_EmptyReason_Throws()
    {
        var s = NewDth();
        Assert.Throws<ArgumentException>(
            () => s.AddAdHocEmbargo(new(10, 0), new(10, 5), ""));
    }

    // ── News ───────────────────────────────────────────────────────────

    [Fact]
    public void News_HighImpact_UsesGlobalDefaults_5Before_15After()
    {
        var s = NewDth();
        var at = new DateTime(2024, 1, 2, 14, 30, 0);
        var e = s.AddNewsEmbargo(at, NewsImpact.High, "FOMC");
        var (from, until) = e.ResolveRange(s);

        Assert.Equal(new TimeOnly(14, 25), from);    // 5 before
        Assert.Equal(new TimeOnly(14, 45), until);   // 15 after
    }

    [Fact]
    public void News_MediumImpact_UsesGlobalDefaults_3Before_10After()
    {
        var s = NewDth();
        var at = new DateTime(2024, 1, 2, 14, 30, 0);
        var e = s.AddNewsEmbargo(at, NewsImpact.Medium, "Jobless claims");
        var (from, until) = e.ResolveRange(s);

        Assert.Equal(new TimeOnly(14, 27), from);    // 3 before
        Assert.Equal(new TimeOnly(14, 40), until);   // 10 after
    }

    [Fact]
    public void News_IsEmbargoed_BeforeWindow_False()
    {
        var s = NewDth();
        var e = s.AddNewsEmbargo(new DateTime(2024, 1, 2, 14, 30, 0), NewsImpact.High, "FOMC");
        Assert.False(e.IsEmbargoed(s, new DateTime(2024, 1, 2, 14, 24, 0)));
    }

    [Fact]
    public void News_IsEmbargoed_InsideWindow_True()
    {
        var s = NewDth();
        var e = s.AddNewsEmbargo(new DateTime(2024, 1, 2, 14, 30, 0), NewsImpact.High, "FOMC");
        Assert.True(e.IsEmbargoed(s, new DateTime(2024, 1, 2, 14, 30, 0)));
        Assert.True(e.IsEmbargoed(s, new DateTime(2024, 1, 2, 14, 44, 0)));
    }

    [Fact]
    public void News_EmptyReason_Throws()
    {
        var s = NewDth();
        Assert.Throws<ArgumentException>(
            () => s.AddNewsEmbargo(new DateTime(2024, 1, 2, 14, 30, 0), NewsImpact.High, ""));
    }

    // ── Anchored ───────────────────────────────────────────────────────

    [Fact]
    public void Anchored_Start_ResolvesAtSessionFrom()
    {
        var s = NewDth();  // 08:00–16:00
        var e = s.AddAnchoredEmbargo(SessionAnchor.Start, TimeSpan.FromMinutes(1), "open-1m");
        var (from, until) = e.ResolveRange(s);

        Assert.Equal(new TimeOnly(8, 0), from);
        Assert.Equal(new TimeOnly(8, 1), until);
    }

    [Fact]
    public void Anchored_End_ResolvesAtSessionUntil()
    {
        var s = NewDth();  // 08:00–16:00
        var e = s.AddAnchoredEmbargo(SessionAnchor.End, TimeSpan.FromMinutes(5), "close-5m");
        var (from, until) = e.ResolveRange(s);

        Assert.Equal(new TimeOnly(15, 55), from);
        Assert.Equal(new TimeOnly(16, 0),  until);
    }

    [Fact]
    public void Anchored_IsEmbargoed_InsideOpening_True()
    {
        var s = NewDth();
        var e = s.AddAnchoredEmbargo(SessionAnchor.Start, TimeSpan.FromMinutes(1), "open-1m");
        Assert.True (e.IsEmbargoed(s, new DateTime(2024, 1, 2, 8, 0, 30)));
        Assert.False(e.IsEmbargoed(s, new DateTime(2024, 1, 2, 8, 1, 0)));
    }

    [Fact]
    public void Anchored_NonPositiveDuration_Throws()
    {
        var s = NewDth();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => s.AddAnchoredEmbargo(SessionAnchor.Start, TimeSpan.Zero, "bad"));
    }

    [Fact]
    public void Anchored_EmptyReason_Throws()
    {
        var s = NewDth();
        Assert.Throws<ArgumentException>(
            () => s.AddAnchoredEmbargo(SessionAnchor.Start, TimeSpan.FromMinutes(1), ""));
    }

    // ── Validation ─────────────────────────────────────────────────────

    [Fact]
    public void IsEmbargoed_NonUnspecifiedKind_Throws()
    {
        var s = NewDth();
        var e = s.AddAdHocEmbargo(new(10, 0), new(10, 5), "blackout");
        Assert.Throws<ArgumentOutOfRangeException>(
            () => e.IsEmbargoed(s, DateTime.UtcNow));
    }
}
