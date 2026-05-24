namespace SquidEyes.Pricing;

/// <summary>
/// Payload for <see cref="CandleSet.CandleClosed"/>. Carries the just-closed
/// <see cref="Candle"/> along with the ET timestamp of the tick that caused the close
/// (useful for distinguishing "a tick rolled us over" from "a brick filled exactly here").
/// </summary>
public sealed class CandleClosedEventArgs : EventArgs
{
    public Candle ClosedCandle { get; }
    public DateTime TriggerET { get; }

    public CandleClosedEventArgs(Candle closedCandle, DateTime triggerET)
    {
        ClosedCandle = closedCandle ?? throw new ArgumentNullException(nameof(closedCandle));

        if (triggerET.Kind != DateTimeKind.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(triggerET), "\"triggerET.Kind\" must be \"Unspecified\".");

        TriggerET = triggerET;
    }
}
