namespace SquidEyes.Pricing;

public enum SessionKind : byte
{
    DTH = 1,   // Day Trading Hours        08:00–16:00 ET
    MTH,       // Morning Trading Hours    08:00–12:00 ET
    RTH        // Regular Trading Hours    09:30–16:00 ET  (CME cash session)
}
