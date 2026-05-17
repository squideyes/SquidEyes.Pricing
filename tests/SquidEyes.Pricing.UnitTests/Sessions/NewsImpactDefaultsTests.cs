using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Sessions;

public class NewsImpactDefaultsTests
{
    [Fact]
    public void Defaults_MatchSpec()
    {
        // Reading the static defaults — also acts as a regression guard if someone changes them.
        Assert.Equal(5,  NewsImpactDefaults.HighBeforeMinutes);
        Assert.Equal(15, NewsImpactDefaults.HighAfterMinutes);
        Assert.Equal(3,  NewsImpactDefaults.MediumBeforeMinutes);
        Assert.Equal(10, NewsImpactDefaults.MediumAfterMinutes);
    }

    [Fact]
    public void For_InvalidImpact_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => NewsImpactDefaults.For((NewsImpact)0));
}
