using System.Globalization;

namespace Earmatch.Core;

/// String helpers with the semantics of Foundation's CharacterSet trimming that the macOS code uses.
internal static class Text
{
    /// Trims spaces and tabs, like `trimmingCharacters(in: .whitespaces)` (not newlines).
    public static string TrimWhitespaces(string text) => TrimWhere(text, IsWhitespace);

    /// Trims spaces, tabs and newlines, like `trimmingCharacters(in: .whitespacesAndNewlines)`.
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
