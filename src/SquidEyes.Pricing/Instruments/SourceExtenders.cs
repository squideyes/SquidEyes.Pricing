namespace SquidEyes.Pricing;

public static class SourceExtenders
{
    public static string ToCode(this Source source) => source switch
    {
        Source.DataBento => "DB",
        _ => throw new ArgumentOutOfRangeException(nameof(source), $"Unknown source: {source}")
    };

    public static Source ParseCode(string code) => code.ToUpperInvariant() switch
    {
        "DB" => Source.DataBento,
        _ => throw new ArgumentException($"Unknown source code: {code}", nameof(code))
    };
}
