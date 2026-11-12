namespace Earmatch.Core;

public sealed class StallBackoff(TimeSpan duration)
{
    private DateTime? _resumeAt;

    public TimeSpan Duration { get; } = duration;

    public bool AllowsPoll(DateTime at) => _resumeAt is not { } resumeAt || at >= resumeAt;

    public void RecordStall(DateTime at) => _resumeAt = at + Duration;
}
