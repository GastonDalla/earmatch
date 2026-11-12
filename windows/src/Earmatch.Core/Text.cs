using System.Globalization;

namespace Earmatch.Core;

internal static class Text
{
    public static string TrimWhitespaces(string text) => TrimWhere(text, IsWhitespace);

    public static string TrimWhitespacesAndNewlines(string text) => TrimWhere(text, char.IsWhiteSpace);

    private static bool IsWhitespace(char character) =>
        character == '\t' || CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.SpaceSeparator;

    private static string TrimWhere(string text, Func<char, bool> trims)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && trims(text[start])) start++;
        while (end > start && trims(text[end - 1])) end--;
        return text[start..end];
    }
}
