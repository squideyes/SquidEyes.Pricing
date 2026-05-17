namespace SquidEyes.Pricing;

internal static class GenericValueExtenders
{
    internal static R Convert<T, R>(this T value, Func<T, R> convert) =>
        convert(value);
}
