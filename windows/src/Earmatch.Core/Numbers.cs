namespace Earmatch.Core;

internal static class Numbers
{
    public static double Round(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

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
