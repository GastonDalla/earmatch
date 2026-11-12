namespace Earmatch.Core;

/// What one live snapshot did to the mix trainer: the mix it finished (to be saved), whether the transition shown in
/// the window changed, and whether nothing was read while a mix is open, so the app must check for the gap that ends it.
public readonly record struct PracticeStep(PracticeResult? Result, bool LiveChanged, bool NeedsGapCheck);

/// The mix trainer fed from the live decks (PracticeController.swift without its threads): it runs a PracticeSession
/// on the live snapshots and publishes the transition being timed at most 15 times a second, at once when a mix starts,
/// ends or its tempos match. Times are seconds on the caller's clock; `dateAtTimeZero` dates them.
public sealed class PracticeTrainer
{
    public const double LivePublishInterval = 1.0 / 15;

    private readonly PracticeSession _session;
    private double _lastLivePublish = double.NegativeInfinity;

    public PracticeTrainer(DateTimeOffset dateAtTimeZero)
    {
        _session = new PracticeSession(dateAtTimeZero);
    }

    /// The transition the window shows.
    public PracticeLiveStatus? Live { get; private set; }

    public bool IsMixing { get; private set; }

    public PracticeStep Ingest(LiveDecksSnapshot snapshot, double time, DateTimeOffset dateAtTimeZero)
    {
        _session.DateAtTimeZero = dateAtTimeZero;
        var result = _session.Ingest(snapshot, time);
        var changed = Publish(time, force: result is not null);
        return new PracticeStep(result, changed, snapshot.Decks.Count == 0 && _session.IsMixing);
    }

    /// Moves the session on to `time` with nothing read (the gap check): it may close the open mix.
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
