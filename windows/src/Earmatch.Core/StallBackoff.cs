namespace Earmatch.Core;

/// After a stalled read of rekordbox, waits a while before polling again.
public sealed class StallBackoff(TimeSpan duration)
{
    private DateTime? _resumeAt;

    public TimeSpan Duration { get; } = duration;

    public bool AllowsPoll(DateTime at) => _resumeAt is not { } resumeAt || at >= resumeAt;

    public void RecordStall(DateTime at) => _resumeAt = at + Duration;
}
