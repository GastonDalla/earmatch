namespace Earmatch.Core;

/// Whether an update found now asks in front or waits in the menus: only right after opening, and never over rekordbox.
public static class UpdatePrompt
{
    public const double LaunchWindow = 120;

    public static bool AsksNow(double secondsSinceLaunch, bool rekordboxIsFrontmost) =>
        secondsSinceLaunch < LaunchWindow && !rekordboxIsFrontmost;
}
