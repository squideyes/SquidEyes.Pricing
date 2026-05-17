namespace SquidEyes.Pricing;

public enum NewsImpact : byte
{
    Medium = 1,
    High,
    Low       // appended (kept Medium=1/High=2 stable); defaults to 0 before/0 after — a
              // present-but-no-op embargo for completeness / record-keeping.
}
