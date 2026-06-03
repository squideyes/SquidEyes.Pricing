namespace SquidEyes.Pricing;

/// <summary>
/// The new value of one book slot, as produced by a quote update. The decoder simply overwrites
/// <c>book[Side][Level]</c> with these values — there is no insert/shift/sort logic. A
/// <see cref="Size"/> of 0 marks the slot empty.
/// </summary>
/// <remarks>
/// Prices are carried as integer ticks (<c>round(price / TickSize)</c>) so they round-trip
/// exactly; multiply by the instrument tick size to recover the real price.
/// </remarks>
public readonly record struct SlotChange(
    BookSide Side,
    int Level,
    int PriceTicks,
    int Size,
    int OrderCount);
