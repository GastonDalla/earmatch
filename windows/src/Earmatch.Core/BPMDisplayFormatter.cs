using System.Globalization;
using System.Text.RegularExpressions;

namespace Earmatch.Core;

public static partial class BPMDisplayFormatter
{
    public static double? Parse(string raw)
    {
        var trimmed = Text.TrimWhitespaces(raw);
        if (!BPMPattern().IsMatch(trimmed)) return null;
        return double.Parse(trimmed, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^[0-9]{1,3}(\.[0-9]{1,2})?$")]
    private static partial Regex BPMPattern();
}
