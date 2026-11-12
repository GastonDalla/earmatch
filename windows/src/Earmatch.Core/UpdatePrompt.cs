namespace Earmatch.Core;

/// What started an update check.
public enum UpdateTrigger
{
    /// The check when Earmatch opens.
    Launch,

    /// The daily check while Earmatch runs.
    Daily,

    /// Check for Updates… in the tray menu or the Setup pane.
    User,
}

/// Whether an update found now asks in front or waits in the menus: only right after opening, and never over rekordbox.
public static class UpdatePrompt
{
    public const double LaunchWindow = 120;

    public const string SkippedVersionKey = "skippedUpdateVersion";

    public static bool IsSkipped(UpdateTrigger trigger, string version, string? skippedVersion) =>
        trigger != UpdateTrigger.User && string.Equals(version, skippedVersion, StringComparison.Ordinal);

    public static bool AsksNow(double secondsSinceLaunch, bool rekordboxIsFrontmost) =>
        secondsSinceLaunch < LaunchWindow && !rekordboxIsFrontmost;

    /// The user's own check always answers; the daily check only marks "Update Available…" in the menus, and the
    /// check when Earmatch opens asks only within the launch window and while rekordbox is not in front.
    public static bool AsksNow(UpdateTrigger trigger, double secondsSinceLaunch, bool rekordboxIsFrontmost) => trigger switch
    {
        UpdateTrigger.User => true,
        UpdateTrigger.Launch => AsksNow(secondsSinceLaunch, rekordboxIsFrontmost),
        _ => false,
    };
}
