using System.Globalization;

namespace SquidEyes.Pricing;

/// <summary>
/// Canonical filename-stem convention used by SquidEyes pricing artifacts:
/// <c>{Symbol}_{yyyyMMdd}_{Contract}_{SourceCode}_{SessionCode}_ET</c> — e.g.
/// <c>ES_20260514_M6_DB_MTH_ET</c>. Consumers append the file extension
/// (<c>.stba</c>, <c>.stba.csv</c>, …) themselves.
/// </summary>
/// <remarks>
/// Both directions are supported so loaders can recover the typed
/// <c>(Symbol, Date, Contract, Source, SessionKind)</c> tuple from a filename without
/// peeking at file contents.
/// </remarks>
public static class PricingFile
{
    /// <summary>
    /// Builds the canonical stem for <paramref name="symbol"/> /
    /// <paramref name="date"/> / <paramref name="contract"/> /
    /// <paramref name="source"/> / <paramref name="session"/>.
    /// </summary>
    public static string BuildStem(
        Symbol symbol, DateOnly date, Contract contract, Source source, SessionKind session) =>
            $"{symbol}_{date:yyyyMMdd}_{contract.Code}_{source.ToCode()}_{session.ToCode()}_ET";

    /// <summary>
    /// Inverse of <see cref="BuildStem"/>. Returns <see langword="null"/> if
    /// <paramref name="stem"/> does not match the canonical 6-segment shape or any
    /// segment fails to parse. Any file extension on <paramref name="stem"/> is stripped
    /// before parsing, so callers can pass either a stem or a full filename.
    /// </summary>
    public static (Symbol Symbol, DateOnly Date, Contract Contract, Source Source, SessionKind Session)?
        TryParseStem(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem))
            return null;

        var firstDot = stem.IndexOf('.');
        if (firstDot >= 0)
            stem = stem[..firstDot];

        var parts = stem.Split('_');
        if (parts.Length != 6)
            return null;

        if (!Enum.TryParse<Symbol>(parts[0], ignoreCase: false, out var symbol))
            return null;

        if (!DateOnly.TryParseExact(parts[1], "yyyyMMdd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            return null;

        Contract contract;
        try { contract = Contract.Create(symbol, parts[2]); }
        catch (ArgumentException) { return null; }

        Source source;
        try { source = SourceExtenders.ParseCode(parts[3]); }
        catch (ArgumentException) { return null; }

        SessionKind session;
        try { session = SessionKindExtenders.ParseCode(parts[4]); }
        catch (ArgumentException) { return null; }

        if (parts[5] != "ET")
            return null;

        return (symbol, date, contract, source, session);
    }
}
