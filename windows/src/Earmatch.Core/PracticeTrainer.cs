namespace Earmatch.Core;

public readonly record struct PracticeStep(PracticeResult? Result, bool LiveChanged, bool NeedsGapCheck);

public sealed class PracticeTrainer
{
    public const double LivePublishInterval = 1.0 / 15;

    private readonly PracticeSession _session;
    private double _lastLivePublish = double.NegativeInfinity;

    public PracticeTrainer(DateTimeOffset dateAtTimeZero)
    {
        _session = new PracticeSession(dateAtTimeZero);
    }

    public PracticeLiveStatus? Live { get; private set; }

    public bool IsMixing { get; private set; }

    public PracticeStep Ingest(LiveDecksSnapshot snapshot, double time, DateTimeOffset dateAtTimeZero)
    {
        _session.DateAtTimeZero = dateAtTimeZero;
        var result = _session.Ingest(snapshot, time);
        var changed = Publish(time, force: result is not null);
        return new PracticeStep(result, changed, snapshot.Decks.Count == 0 && _session.IsMixing);
    }

    public (PracticeResult? Result, bool LiveChanged) Advance(double time, DateTimeOffset dateAtTimeZero)
    {
        _session.DateAtTimeZero = dateAtTimeZero;
        var result = _session.Advance(time);
        return (result, Publish(time, force: true));
    }

    private bool Publish(double time, bool force)
    {
        var next = _session.Live;
        IsMixing = next is not null;
        if (next == Live) return false;
        var isTransition = (next is null) != (Live is null) || next?.IsMatched != Live?.IsMatched;
        if (!force && !isTransition && time - _lastLivePublish < LivePublishInterval) return false;
        _lastLivePublish = time;
        Live = next;
        return true;
    }
}
