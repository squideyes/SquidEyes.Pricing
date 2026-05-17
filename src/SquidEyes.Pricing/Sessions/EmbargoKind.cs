namespace SquidEyes.Pricing;

public enum EmbargoKind : byte
{
    AdHoc = 1,    // explicit ET (From, Until) window
    News,         // anchored to a DateTime with impact-driven minutes before/after
    Anchored      // anchored to the start or end of the owning Session, with a Duration
}
