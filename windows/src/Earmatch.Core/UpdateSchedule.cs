namespace Earmatch.Core;

/// When Earmatch checks for updates by itself, as Sparkle does on macOS: when it opens, then once a day while it runs.
public static class UpdateSchedule
{
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    /// Whether a day has passed since the last check (any check, the user's too). A clock set back before the last
    /// check counts as due, so a wrong clock never stops the checks.
    public static bool IsDue(DateTimeOffset? lastCheck, DateTimeOffset now) =>
        lastCheck is not { } last || now < last || now - last >= Interval;
}
