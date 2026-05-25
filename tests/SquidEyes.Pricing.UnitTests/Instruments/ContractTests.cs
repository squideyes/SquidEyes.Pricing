using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Instruments;

public class ContractTests
{
    [Theory]
    [InlineData(Symbol.ES, "H26", 'H', 26)]
    [InlineData(Symbol.NQ, "Z25", 'Z', 25)]
    [InlineData(Symbol.CL, "F24", 'F', 24)]
    [InlineData(Symbol.GC, "G26", 'G', 26)]
    [InlineData(Symbol.MES, "H26", 'H', 26)]
    [InlineData(Symbol.MNQ, "M26", 'M', 26)]
    [InlineData(Symbol.RTY, "U26", 'U', 26)]
    [InlineData(Symbol.M2K, "Z25", 'Z', 25)]
    [InlineData(Symbol.MGC, "J26", 'J', 26)]
    [InlineData(Symbol.MCL, "K26", 'K', 26)]
    public void Create_ValidCode_SetsProperties(Symbol symbol, string code, char month, int year)
    {
        var contract = Contract.Create(symbol, code);
        Assert.Equal(symbol, contract.Symbol);
        Assert.Equal(month, contract.Month);
        Assert.Equal(year, contract.Year);
    }

    [Fact]
    public void Create_LowercaseCode_Works()
    {
        var contract = Contract.Create(Symbol.ES, "h26");
        Assert.Equal('H', contract.Month);
    }

    [Fact]
    public void Code_ReturnsFormattedString()
    {
        var contract = Contract.Create(Symbol.ES, "H26");
        Assert.Equal("H26", contract.Code);
    }

    [Fact]
    public void Code_SingleDigitYear_PadsWithZero()
    {
        var contract = Contract.Create(Symbol.ES, "H6");
        Assert.Equal("H06", contract.Code);
    }

    [Fact]
    public void Create_InvalidMonth_ForQuarterly_Throws()
    {
        Assert.Throws<ArgumentException>(() => Contract.Create(Symbol.ES, "F26"));
    }

    [Fact]
    public void Create_InvalidMonth_ForGold_Throws()
    {
        Assert.Throws<ArgumentException>(() => Contract.Create(Symbol.GC, "H26"));
    }

    [Fact]
    public void Create_MGC_FollowsGoldMonthSet()
    {
        // MGC inherits GC's month set (G, J, M, Q, V, Z); H is not valid.
        Assert.Throws<ArgumentException>(() => Contract.Create(Symbol.MGC, "H26"));
        var ok = Contract.Create(Symbol.MGC, "Z26");
        Assert.Equal('Z', ok.Month);
    }

    [Fact]
    public void Create_MCL_AcceptsAnyMonth()
    {
        // MCL inherits CL's full-month set; F (Jan) is valid.
        var jan = Contract.Create(Symbol.MCL, "F26");
        Assert.Equal('F', jan.Month);
        var nov = Contract.Create(Symbol.MCL, "X26");
        Assert.Equal('X', nov.Month);
    }

    [Fact]
    public void Create_MES_QuarterlyOnly()
    {
        // Micro equity index futures inherit the default quarterly set (H, M, U, Z).
        Assert.Throws<ArgumentException>(() => Contract.Create(Symbol.MES, "F26"));
        var ok = Contract.Create(Symbol.MES, "H26");
        Assert.Equal('H', ok.Month);
    }

    [Fact]
    public void Create_EmptyCode_Throws()
    {
        Assert.Throws<ArgumentException>(() => Contract.Create(Symbol.ES, ""));
    }

    [Fact]
    public void Create_TooLong_Throws()
    {
        Assert.Throws<ArgumentException>(() => Contract.Create(Symbol.ES, "H260"));
    }

    [Fact]
    public void Create_InvalidYear_Throws()
    {
        Assert.Throws<ArgumentException>(() => Contract.Create(Symbol.ES, "HX"));
    }

    [Fact]
    public void Equals_SameContract_True()
    {
        var a = Contract.Create(Symbol.ES, "H26");
        var b = Contract.Create(Symbol.ES, "H26");
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equals_DifferentContract_False()
    {
        var a = Contract.Create(Symbol.ES, "H26");
        var b = Contract.Create(Symbol.ES, "M26");
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equals_DifferentSymbol_False()
    {
        var a = Contract.Create(Symbol.ES, "H26");
        var b = Contract.Create(Symbol.NQ, "H26");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GetHashCode_SameContract_SameHash()
    {
        var a = Contract.Create(Symbol.ES, "H26");
        var b = Contract.Create(Symbol.ES, "H26");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsCode()
    {
        var contract = Contract.Create(Symbol.ES, "H26");
        Assert.Equal("H26", contract.ToString());
    }

    [Fact]
    public void Equals_BoxedObject_Works()
    {
        var a = Contract.Create(Symbol.ES, "H26");
        object b = Contract.Create(Symbol.ES, "H26");
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_BoxedNonContract_False()
    {
        var a = Contract.Create(Symbol.ES, "H26");
        Assert.False(a.Equals("not a contract"));
    }
}
