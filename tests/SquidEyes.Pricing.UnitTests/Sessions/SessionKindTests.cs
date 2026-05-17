using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Sessions;

public class SessionKindTests
{
    [Theory]
    [InlineData(SessionKind.DTH, 8, 0,  16, 0)]
    [InlineData(SessionKind.MTH, 8, 0,  12, 0)]
    [InlineData(SessionKind.RTH, 9, 30, 16, 0)]
    public void ToTimes_ReturnsExpectedWindow(SessionKind kind, int fromH, int fromM, int untilH, int untilM)
    {
        var (from, until) = kind.ToTimes();
        Assert.Equal(new TimeOnly(fromH, fromM), from);
        Assert.Equal(new TimeOnly(untilH, untilM), until);
    }

    [Theory]
    [InlineData(SessionKind.DTH, "DTH")]
    [InlineData(SessionKind.MTH, "MTH")]
    [InlineData(SessionKind.RTH, "RTH")]
    public void ToCode_ReturnsExpectedString(SessionKind kind, string expected) =>
        Assert.Equal(expected, kind.ToCode());

    [Theory]
    [InlineData("DTH", SessionKind.DTH)]
    [InlineData("dth", SessionKind.DTH)]
    [InlineData("MTH", SessionKind.MTH)]
    [InlineData("RTH", SessionKind.RTH)]
    [InlineData("rth", SessionKind.RTH)]
    public void ParseCode_KnownCode_ReturnsKind(string code, SessionKind expected) =>
        Assert.Equal(expected, SessionKindExtenders.ParseCode(code));

    [Fact]
    public void ParseCode_UnknownCode_Throws() =>
        Assert.Throws<ArgumentException>(() => SessionKindExtenders.ParseCode("XYZ"));

    [Fact]
    public void ToCode_InvalidEnumValue_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ((SessionKind)0).ToCode());

    [Fact]
    public void ToTimes_InvalidEnumValue_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ((SessionKind)0).ToTimes());
}
