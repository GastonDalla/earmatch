using System.Text;

namespace Earmatch.Core;

public enum ReleaseNoteKind
{
    Heading,
    Bullet,
    Paragraph,
}

public sealed record ReleaseNoteBlock(ReleaseNoteKind Kind, string Text);

public static class ReleaseNotes
{
    public static IReadOnlyList<ReleaseNoteBlock> Parse(string? markdown)
    {
        var blocks = new List<ReleaseNoteBlock>();
        if (string.IsNullOrWhiteSpace(markdown)) return blocks;
        ReleaseNoteKind? kind = null;
        var text = new StringBuilder();

        void Flush()
        {
            if (kind is { } current && text.Length > 0) blocks.Add(new ReleaseNoteBlock(current, Inline(text.ToString())));
            kind = null;
            text.Clear();
        }

        foreach (var raw in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                Flush();
                continue;
            }
            if (line.StartsWith('#'))
            {
                Flush();
                blocks.Add(new ReleaseNoteBlock(ReleaseNoteKind.Heading, Inline(line.TrimStart('#').Trim())));
                continue;
            }
            if (line.Length > 1 && line[0] is '-' or '*' or '+' && line[1] == ' ')
            {
                Flush();
                kind = ReleaseNoteKind.Bullet;
                text.Append(line.AsSpan(2).Trim());
                continue;
            }
            if (kind is null) kind = ReleaseNoteKind.Paragraph;
            else text.Append(' ');
            text.Append(line);
        }
        Flush();
        return blocks;
    }

    private static string Inline(string text)
    {
        var result = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '[' && LinkText(text, index) is var (label, end) && end > index)
            {
                result.Append(label);
                index = end;
                continue;
            }
            if (character is '*' or '_' && index + 1 < text.Length && text[index + 1] == character)
            {
                index++;
                continue;
            }
            if (character == '`') continue;
            result.Append(character);
        }
        return result.ToString().Trim();
    }

    private static (string Label, int End) LinkText(string text, int start)
    {
        var close = text.IndexOf("](", start, StringComparison.Ordinal);
        if (close < 0) return ("", -1);
        var end = text.IndexOf(')', close + 2);
        if (end < 0) return ("", -1);
        return (text[(start + 1)..close], end);
    }
}
