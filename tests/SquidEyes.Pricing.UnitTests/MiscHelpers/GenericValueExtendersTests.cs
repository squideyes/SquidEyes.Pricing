using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.MiscHelpers;

public class GenericValueExtendersTests
{
    [Fact]
    public void Convert_ProjectsValueThroughLambda()
    {
        var result = 42.Convert(x => x * 2);
        Assert.Equal(84, result);
    }

    [Fact]
    public void Convert_PreservesReferenceType()
    {
        var input = "hello";
        var result = input.Convert(s => s.ToUpperInvariant());
        Assert.Equal("HELLO", result);
    }

    [Fact]
    public void Convert_ChangesType()
    {
        var result = new DateOnly(2026, 3, 15).Convert(d => d.ToString("yyyy-MM-dd"));
        Assert.Equal("2026-03-15", result);
    }

    [Fact]
    public void Convert_NullableValue_PassedThrough()
    {
        int? input = null;
        var result = input.Convert(x => x.HasValue ? "set" : "unset");
        Assert.Equal("unset", result);
    }

    [Fact]
    public void Convert_LambdaInvokedOnce()
    {
        var invocations = 0;
        var _ = 5.Convert(x => { invocations++; return x; });
        Assert.Equal(1, invocations);
    }
}
