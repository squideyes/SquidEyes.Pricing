using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.Instruments;

public class SymbolContractParserTests
{
    [Theory]
    [InlineData("ESH5", 2025, Symbol.ES, "H25")]
    [InlineData("ESM5", 2025, Symbol.ES, "M25")]
    [InlineData("ESU5", 2025, Symbol.ES, "U25")]
    [InlineData("ESZ5", 2025, Symbol.ES, "Z25")]
    [InlineData("ESH6", 2025, Symbol.ES, "H26")]
    [InlineData("NQH5", 2025, Symbol.NQ, "H25")]
    [InlineData("ESH26", 2025, Symbol.ES, "H26")]
    [InlineData("MESH26", 2025, Symbol.MES, "H26")]
    [InlineData("MNQM6", 2025, Symbol.MNQ, "M26")]
    [InlineData("RTYU5", 2025, Symbol.RTY, "U25")]
    [InlineData("M2KZ25", 2025, Symbol.M2K, "Z25")]
    [InlineData("MGCJ26", 2025, Symbol.MGC, "J26")]
    [InlineData("MCLK26", 2025, Symbol.MCL, "K26")]
    public void TryParse_FromExpandableNames(string segment, int dataYear, Symbol expectedSymbol, string expectedContract)
    {
        var result = SymbolContractParser.TryParse(segment, new DateOnly(dataYear, 6, 15));
        Assert.NotNull(result);
        Assert.Equal(expectedSymbol, result.Value.Symbol);
        Assert.Equal(expectedContract, result.Value.Contract.Code);
    }

    [Theory]
    [InlineData("ES.c.0", 2025)]
    [InlineData("ES", 2025)]
    [InlineData("XX5", 2025)]
    [InlineData("", 2025)]
    [InlineData("ESF5", 2025)]
    public void TryParse_Unparseable_ReturnsNull(string segment, int dataYear)
    {
        var result = SymbolContractParser.TryParse(segment, new DateOnly(dataYear, 6, 15));
        Assert.Null(result);
    }
}
