namespace SquidEyes.Pricing;

/// <summary>
/// What kind of price a tick represents. Flag-shaped so consumers can test for "any trade" via
/// <c>(kind &amp; PriceKind.Trade) != 0</c>, while individual ticks always carry exactly one of the
/// four primitive members (Bid, Ask, TradeBid, TradeAsk).
///
/// Aggressor convention on trade ticks:
///   TradeBid — seller hit the resting bid (down-tick / "hit")
///   TradeAsk — buyer lifted the resting ask (up-tick / "lift")
///
/// Values start at 1 so that <c>default(PriceKind)</c> (==0) is invalid and easy to detect.
/// </summary>
[Flags]
public enum PriceKind : byte
{
    Bid      = 1,    // 0b0001
    Ask      = 2,    // 0b0010
    TradeBid = 4,    // 0b0100
    TradeAsk = 8,    // 0b1000
    Trade    = TradeBid | TradeAsk   // 0b1100 — mask only, never a stored single-tick value
}
