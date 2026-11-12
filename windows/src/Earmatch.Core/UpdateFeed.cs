using System.Text.RegularExpressions;

namespace Earmatch.Core;

public enum UpdateFeedKind
{
    /// The GitHub releases of GastonDalla/earmatch: each release v<version> carries the macOS DMG and the Windows
    /// packages, one feed (releases.<channel>.json) per Windows architecture.
    GitHub,

    /// A local folder that `vpk pack` wrote, for testing an update before it is published.
    LocalFolder,

    /// EARMATCH_UPDATE_FEED holds something else; Earmatch then checks nothing rather than guess another feed.
    Invalid,
}

/// Where Earmatch for Windows looks for updates (SUFeedURL on macOS). EARMATCH_UPDATE_FEED can point an installed copy
/// at a local folder to test an update end to end; it never names another web address.
public sealed partial record UpdateFeed(UpdateFeedKind Kind, string Location)
{
    public const string RepositoryUrl = "https://github.com/GastonDalla/earmatch";
    public const string OverrideVariable = "EARMATCH_UPDATE_FEED";

    public static UpdateFeed GitHub { get; } = new(UpdateFeedKind.GitHub, RepositoryUrl);

    /// The feed as the diagnostics name it: the repository without its scheme, or the folder.
    public string DisplayName => Kind == UpdateFeedKind.GitHub ? Location["https://".Length..] : Location;

    /// The feed for the value of EARMATCH_UPDATE_FEED: GitHub when it is unset, a local folder for a full Windows path
    /// (C:\…, D:/…, \\server\share\…), and Invalid for anything else, web addresses included.
    public static UpdateFeed Resolve(string? overrideValue)
    {
        var value = overrideValue?.Trim() ?? "";
        if (value.Length == 0) return GitHub;
        return FullWindowsPath().IsMatch(value) ? new UpdateFeed(UpdateFeedKind.LocalFolder, value) : new UpdateFeed(UpdateFeedKind.Invalid, value);
    }

    [GeneratedRegex(@"^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+)")]
    private static partial Regex FullWindowsPath();
}
