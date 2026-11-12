namespace Earmatch.Core;

public enum UpdateTrigger
{
    Launch,

    Daily,

    User,
}

public static class UpdatePrompt
{
    public const double LaunchWindow = 120;

    public const string SkippedVersionKey = "skippedUpdateVersion";

    public static bool IsSkipped(UpdateTrigger trigger, string version, string? skippedVersion) =>
        trigger != UpdateTrigger.User && string.Equals(version, skippedVersion, StringComparison.Ordinal);

    public static bool AsksNow(double secondsSinceLaunch, bool rekordboxIsFrontmost) =>
        secondsSinceLaunch < LaunchWindow && !rekordboxIsFrontmost;

    public static bool AsksNow(UpdateTrigger trigger, double secondsSinceLaunch, bool rekordboxIsFrontmost) => trigger switch
    {
        UpdateTrigger.User => true,
        UpdateTrigger.Launch => AsksNow(secondsSinceLaunch, rekordboxIsFrontmost),
        _ => false,
    };
}
