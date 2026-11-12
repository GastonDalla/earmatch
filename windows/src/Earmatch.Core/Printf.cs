using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Earmatch.Core;

internal static partial class Printf
{
    [GeneratedRegex("""%(?:(\d+)\$)?([-+ #0']*)(\d+)?(?:\.(\d+))?(hh|h|ll|l|q|z|t|j|L)?([@dDiuUxXoOfFeEgGcCsSpaA%])""")]
    private static partial Regex Specifier();

    public static string Format(string format, CultureInfo culture, IReadOnlyList<object?> arguments)
    {
        var next = 0;
        return Specifier().Replace(format, match =>
        {
            var conversion = match.Groups[6].Value;
            if (conversion == "%") return "%";
            var index = match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) - 1 : next;
            next = index + 1;
            if (index < 0 || index >= arguments.Count) return match.Value;
            var flags = match.Groups[2].Value;
            var precision = match.Groups[4].Success ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : (int?)null;
            var argument = arguments[index];
            string text;
            var isNumber = true;
            switch (conversion)
            {
                case "@" or "s" or "S":
                    text = Convert.ToString(argument, culture) ?? "(null)";
                    isNumber = false;
                    break;
                case "d" or "D" or "i":
                    text = Convert.ToInt64(argument, culture).ToString("D", culture);
                    break;
                case "u" or "U":
                    text = Convert.ToUInt64(argument, culture).ToString("D", culture);
                    break;
                case "x":
                    text = Convert.ToInt64(argument, culture).ToString("x", culture);
                    break;
                case "X":
                    text = Convert.ToInt64(argument, culture).ToString("X", culture);
                    break;
                case "f" or "F":
                    text = Convert.ToDouble(argument, culture).ToString("F" + (precision ?? 6), culture);
                    break;
                case "e" or "E":
                    text = Convert.ToDouble(argument, culture).ToString(conversion + (precision ?? 6), culture);
                    break;
                case "c" or "C":
                    text = Convert.ToChar(argument, culture).ToString();
                    isNumber = false;
                    break;
                default:
                    return match.Value;
            }
            if (isNumber && flags.Contains('+') && !text.StartsWith(culture.NumberFormat.NegativeSign, StringComparison.Ordinal))
            {
                text = culture.NumberFormat.PositiveSign + text;
            }
            else if (isNumber && flags.Contains(' ') && !text.StartsWith(culture.NumberFormat.NegativeSign, StringComparison.Ordinal))
            {
                text = " " + text;
            }
            return Pad(text, match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 0, flags, isNumber, culture);
        });
    }

    private static string Pad(string text, int width, string flags, bool isNumber, CultureInfo culture)
    {
        if (text.Length >= width) return text;
        if (flags.Contains('-')) return text.PadRight(width);
        if (!isNumber || !flags.Contains('0')) return text.PadLeft(width);
        var sign = text.StartsWith(culture.NumberFormat.NegativeSign, StringComparison.Ordinal) || text.StartsWith('+') || text.StartsWith(' ')
            ? text[..1]
            : "";
        return new StringBuilder(sign).Append('0', width - text.Length).Append(text.AsSpan(sign.Length)).ToString();
    }
}
