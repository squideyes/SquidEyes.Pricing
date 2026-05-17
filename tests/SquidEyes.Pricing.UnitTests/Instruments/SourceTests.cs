using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Instruments;

public class SourceTests
{
    [Theory]
    [InlineData(Source.DataBento, "DB")]
    public void ToCode_ReturnsExpectedString(Source source, string expected) =>
        Assert.Equal(expected, source.ToCode());

    [Theory]
    [InlineData("DB", Source.DataBento)]
    [InlineData("db", Source.DataBento)]
    public void ParseCode_KnownCode_ReturnsSource(string code, Source expected) =>
        Assert.Equal(expected, SourceExtenders.ParseCode(code));

    [Fact]
    public void ParseCode_UnknownCode_Throws() =>
        Assert.Throws<ArgumentException>(() => SourceExtenders.ParseCode("XYZ"));

    [Fact]
    public void ToCode_InvalidEnumValue_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ((Source)0).ToCode());
}
