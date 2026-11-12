using System.Text.RegularExpressions;

namespace Earmatch.Core;

public enum UpdateFeedKind
{
    GitHub,

    LocalFolder,

    Invalid,
}

public sealed partial record UpdateFeed(UpdateFeedKind Kind, string Location)
{
    public const string RepositoryUrl = "https://github.com/GastonDalla/earmatch";
    public const string OverrideVariable = "EARMATCH_UPDATE_FEED";

    public static UpdateFeed GitHub { get; } = new(UpdateFeedKind.GitHub, RepositoryUrl);

    public string DisplayName => Kind == UpdateFeedKind.GitHub ? Location["https://".Length..] : Location;

    public static UpdateFeed Resolve(string? overrideValue)
    {
        var value = overrideValue?.Trim() ?? "";
        if (value.Length == 0) return GitHub;
        return FullWindowsPath().IsMatch(value) ? new UpdateFeed(UpdateFeedKind.LocalFolder, value) : new UpdateFeed(UpdateFeedKind.Invalid, value);
    }

    [GeneratedRegex(@"^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+)")]
    private static partial Regex FullWindowsPath();
}
