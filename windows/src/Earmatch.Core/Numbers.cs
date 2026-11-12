namespace Earmatch.Core;

/// Swift's rounding and modulo semantics, which the macOS code relies on.
internal static class Numbers
{
    /// Rounds half away from zero, like Swift's `rounded()`.
    public static double Round(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

    /// The remainder with the sign of the divisor, like the macOS `positiveModulo`.
    public static double PositiveModulo(double value, double divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }

    public static int PositiveModulo(int value, int divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }
}
