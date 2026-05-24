using SquidEyes.Pricing;

namespace SquidEyes.Pricing.UnitTests.MiscHelpers;

public class PricingFileTests
{
    [Fact]
    public void BuildStem_ProducesCanonicalShape()
    {
        var stem = PricingFile.BuildStem(
            Symbol.ES,
            new DateOnly(2026, 5, 14),
            Contract.Create(Symbol.ES, "M26"),
            Source.DataBento,
            SessionKind.MTH);

        Assert.Equal("ES_20260514_M26_DB_MTH_ET", stem);
    }

    [Fact]
    public void BuildStem_DthSession()
    {
        var stem = PricingFile.BuildStem(
            Symbol.NQ,
            new DateOnly(2026, 1, 2),
            Contract.Create(Symbol.NQ, "H26"),
            Source.DataBento,
            SessionKind.DTH);

        Assert.Equal("NQ_20260102_H26_DB_DTH_ET", stem);
    }

    [Theory]
    [InlineData("ES_20260514_M26_DB_MTH_ET", "ES", 2026, 5, 14, "M26", "MTH")]
    [InlineData("NQ_20260102_H26_DB_DTH_ET", "NQ", 2026, 1, 2,  "H26", "DTH")]
    [InlineData("CL_20240603_N4_DB_RTH_ET",  "CL", 2024, 6, 3,  "N04", "RTH")]
    public void TryParseStem_RoundTripsCanonicalStems(
        string stem, string symbolName, int y, int m, int d, string contract, string session)
    {
        var parsed = PricingFile.TryParseStem(stem);

        Assert.NotNull(parsed);
        var (symbol, date, parsedContract, source, parsedSession) = parsed!.Value;
        Assert.Equal(Enum.Parse<Symbol>(symbolName), symbol);
        Assert.Equal(new DateOnly(y, m, d), date);
        Assert.Equal(contract, parsedContract.Code);
        Assert.Equal(Source.DataBento, source);
        Assert.Equal(Enum.Parse<SessionKind>(session), parsedSession);
    }

    [Fact]
    public void TryParseStem_StripsTrailingExtension()
    {
        var parsed = PricingFile.TryParseStem("ES_20260514_M26_DB_MTH_ET.stba.csv");

        Assert.NotNull(parsed);
        Assert.Equal(Symbol.ES, parsed!.Value.Symbol);
        Assert.Equal(SessionKind.MTH, parsed.Value.Session);
    }

    [Fact]
    public void RoundTrip_BuildThenParse_PreservesAllFields()
    {
        var symbol = Symbol.GC;
        var date = new DateOnly(2027, 11, 17);
        var contract = Contract.Create(Symbol.GC, "Z27");
        var source = Source.DataBento;
        var session = SessionKind.RTH;

        var stem = PricingFile.BuildStem(symbol, date, contract, source, session);
        var parsed = PricingFile.TryParseStem(stem);

        Assert.NotNull(parsed);
        Assert.Equal(symbol, parsed!.Value.Symbol);
        Assert.Equal(date, parsed.Value.Date);
        Assert.Equal(contract, parsed.Value.Contract);
        Assert.Equal(source, parsed.Value.Source);
        Assert.Equal(session, parsed.Value.Session);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("ES_20260514_M26_DB_MTH")]                  // missing _ET suffix
    [InlineData("ES_20260514_M26_DB_MTH_XX")]               // bad trailing token
    [InlineData("ZZ_20260514_M26_DB_MTH_ET")]               // unknown symbol
    [InlineData("ES_2026-05-14_M26_DB_MTH_ET")]             // wrong date format
    [InlineData("ES_20260514_X9X_DB_MTH_ET")]               // malformed contract
    [InlineData("ES_20260514_M26_XX_MTH_ET")]               // unknown source
    [InlineData("ES_20260514_M26_DB_XYZ_ET")]               // unknown session
    [InlineData("ES_20260514_M26_DB_MTH_ET_EXTRA")]         // too many segments
    public void TryParseStem_RejectsMalformedInput(string? stem)
    {
        Assert.Null(PricingFile.TryParseStem(stem!));
    }
}
