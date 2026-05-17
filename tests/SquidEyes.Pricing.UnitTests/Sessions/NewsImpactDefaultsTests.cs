using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Sessions;

public class NewsImpactDefaultsTests
{
    [Fact]
    public void Defaults_MatchSpec()
    {
        // Reading the static defaults — also acts as a regression guard if someone changes them.
        Assert.Equal(new PrePost(5, 15), NewsImpactDefaults.High);
        Assert.Equal(new PrePost(3, 10), NewsImpactDefaults.Medium);
        Assert.Equal(new PrePost(1,  2), NewsImpactDefaults.Low);
    }

    [Fact]
    public void For_InvalidImpact_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => NewsImpactDefaults.For((NewsImpact)0));
}
