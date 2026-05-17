using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.MiscHelpers;

public class PrePostTests
{
    [Fact]
    public void Ctor_AssignsPositionalProperties()
    {
        var pp = new PrePost(5, 15);
        Assert.Equal(5,  pp.Pre);
        Assert.Equal(15, pp.Post);
    }

    [Fact]
    public void Deconstruct_YieldsBothComponents()
    {
        var (pre, post) = new PrePost(3, 10);
        Assert.Equal(3,  pre);
        Assert.Equal(10, post);
    }

    [Fact]
    public void Equality_ValueBased()
    {
        Assert.Equal(new PrePost(1, 2), new PrePost(1, 2));
        Assert.NotEqual(new PrePost(1, 2), new PrePost(2, 1));
    }

    [Fact]
    public void Default_IsZeroZero()
    {
        // Sanity check — default(PrePost) is a real, usable value.
        var pp = default(PrePost);
        Assert.Equal(0, pp.Pre);
        Assert.Equal(0, pp.Post);
    }
}
