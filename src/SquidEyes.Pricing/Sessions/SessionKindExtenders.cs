namespace SquidEyes.Pricing;

public static class SessionKindExtenders
{
    public static (TimeOnly From, TimeOnly Until) ToTimes(this SessionKind kind) => kind switch
    {
        SessionKind.DTH => (new(8, 0),   new(16, 0)),
        SessionKind.MTH => (new(8, 0),   new(12, 0)),
        SessionKind.RTH => (new(9, 30),  new(16, 0)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), $"Unknown SessionKind: {kind}")
    };

    public static string ToCode(this SessionKind kind) => kind switch
    {
        SessionKind.DTH => "DTH",
        SessionKind.MTH => "MTH",
        SessionKind.RTH => "RTH",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), $"Unknown SessionKind: {kind}")
    };

    public static SessionKind ParseCode(string code) => code.ToUpperInvariant() switch
    {
        "DTH" => SessionKind.DTH,
        "MTH" => SessionKind.MTH,
        "RTH" => SessionKind.RTH,
        _ => throw new ArgumentException($"Unknown SessionKind code: {code}", nameof(code))
    };
}
