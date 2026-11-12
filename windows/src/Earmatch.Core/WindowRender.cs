using System.Globalization;

namespace Earmatch.Core;

public enum WindowRenderProblemKind
{
    MissingDirectory,
    InvalidSize,
    BelowMinimum,
}

public sealed class WindowRenderProblem(WindowRenderProblemKind kind, string? size = null) : Exception(Describe(kind, size))
{
    public const string Usage = "Usage: Earmatch --render-window <directory> [--render-size default|minimum|WIDTHxHEIGHT]";

    public WindowRenderProblemKind Kind { get; } = kind;

    public string? Size { get; } = size;

    private static string Describe(WindowRenderProblemKind kind, string? size)
    {
        var reason = kind switch
        {
            WindowRenderProblemKind.MissingDirectory => "The output directory is missing.",
            WindowRenderProblemKind.InvalidSize => $"The size \"{size}\" is not default, minimum or WIDTHxHEIGHT.",
            _ => $"The size {size} is smaller than the window's minimum size.",
        };
        return $"{reason}\n{Usage}";
    }
}

public sealed record WindowRenderRequest(string Directory, WindowSize Size)
{
    public const string Flag = "--render-window";
    public const string SizeFlag = "--render-size";

    public static WindowRenderRequest? Parse(IReadOnlyList<string> arguments)
    {
        var flagIndex = IndexOf(arguments, Flag);
        if (flagIndex < 0) return null;
        var pathIndex = flagIndex + 1;
        if (pathIndex >= arguments.Count || arguments[pathIndex].StartsWith("--", StringComparison.Ordinal))
        {
            throw new WindowRenderProblem(WindowRenderProblemKind.MissingDirectory);
        }
        var size = MainWindowMetrics.DefaultSize;
        var sizeIndex = IndexOf(arguments, SizeFlag);
        if (sizeIndex >= 0) size = ReadSize(sizeIndex + 1 < arguments.Count ? arguments[sizeIndex + 1] : "");
        return new WindowRenderRequest(Path.GetFullPath(arguments[pathIndex]), size);
    }

    private static int IndexOf(IReadOnlyList<string> arguments, string flag)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index] == flag) return index;
        }
        return -1;
    }

    private static WindowSize ReadSize(string value)
    {
        switch (value)
        {
            case "default": return MainWindowMetrics.DefaultSize;
            case "minimum": return MainWindowMetrics.MinimumSize;
        }
        var parts = value.Split('x');
        if (parts.Length != 2 || !TryParseInt(parts[0], out var width) || !TryParseInt(parts[1], out var height) || width <= 0 || height <= 0)
        {
            throw new WindowRenderProblem(WindowRenderProblemKind.InvalidSize, value);
        }
        if (width < MainWindowMetrics.MinimumSize.Width || height < MainWindowMetrics.MinimumSize.Height)
        {
            throw new WindowRenderProblem(WindowRenderProblemKind.BelowMinimum, value);
        }
        return new WindowSize(width, height);
    }

    private static bool TryParseInt(string text, out int value) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
}

public sealed record WindowRenderShot(string FileName, MainWindowPane Pane, int DeckCount)
{
    public const double Scale = 2;

    public static IReadOnlyList<WindowRenderShot> All { get; } =
    [
        new("live.png", MainWindowPane.Live, 2),
        new("overlay.png", MainWindowPane.Overlay, 2),
        new("practice.png", MainWindowPane.Practice, 2),
        new("setup.png", MainWindowPane.Setup, 2),
        new("live-4deck.png", MainWindowPane.Live, 4),
    ];
}
