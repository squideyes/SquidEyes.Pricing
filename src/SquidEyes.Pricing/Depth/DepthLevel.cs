namespace SquidEyes.Pricing;

/// <summary>
/// A single price level in a <see cref="DepthBook"/> ladder: the (real) price, the resting
/// size, and the order count at that price. An empty slot (book shorter than 10 on that side)
/// has <see cref="Size"/> == 0.
/// </summary>
public readonly record struct DepthLevel(double Price, int Size, int OrderCount)
{
    /// <summary>True when the slot is unoccupied (size 0).</summary>
    public bool IsEmpty => Size == 0;
}
