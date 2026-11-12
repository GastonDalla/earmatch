namespace Earmatch.Core;

public static class UpdateSchedule
{
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    public static bool IsDue(DateTimeOffset? lastCheck, DateTimeOffset now) =>
        lastCheck is not { } last || now < last || now - last >= Interval;
}
