namespace Earmatch.Core;

/// One timed transition: how long both decks played together, how fast the tempos matched and how far the beats drifted.
public sealed record PracticeMix(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int OutgoingDeck,
    int IncomingDeck,
    double BothPlayingSeconds,
    double? SecondsToMatch,
    double? TempoDriftBPM,
    double? MeanPhaseErrorMilliseconds,
    double? WorstPhaseErrorMilliseconds);

public readonly record struct PracticeTraceSample(double Seconds, double ErrorMilliseconds);

/// The phase error over the last transition, for its replay.
public sealed record PracticeTrace(Guid MixID, double DurationSeconds, double? MatchedAtSeconds, ValueList<PracticeTraceSample> Samples)
{
    public double? ErrorMilliseconds(double seconds)
    {
        if (Samples.Count == 0) return null;
        var first = Samples[0];
        var last = Samples[^1];
        if (seconds <= first.Seconds) return first.ErrorMilliseconds;
        if (seconds >= last.Seconds) return last.ErrorMilliseconds;
        var low = 0;
        var high = Samples.Count - 1;
        while (high - low > 1)
        {
            var middle = (low + high) / 2;
            if (Samples[middle].Seconds <= seconds) low = middle;
            else high = middle;
        }
        var before = Samples[low];
        var after = Samples[high];
        var span = after.Seconds - before.Seconds;
        if (span <= 0) return before.ErrorMilliseconds;
        return before.ErrorMilliseconds + (after.ErrorMilliseconds - before.ErrorMilliseconds) * (seconds - before.Seconds) / span;
    }

    /// The largest error once the tempos matched; none when they never did.
    public PracticeTraceSample? WorstSample
    {
        get
        {
            var matched = MatchedAtSeconds ?? double.PositiveInfinity;
            PracticeTraceSample? worst = null;
            foreach (var sample in Samples)
            {
                if (sample.Seconds < matched) continue;
                if (worst is null || Math.Abs(sample.ErrorMilliseconds) > Math.Abs(worst.Value.ErrorMilliseconds)) worst = sample;
            }
            return worst;
        }
    }
}

public sealed record PracticeResult(PracticeMix Mix, PracticeTrace Trace);

/// The transition being timed right now.
public sealed record PracticeLiveStatus(
    int OutgoingDeck,
    int IncomingDeck,
    double ElapsedSeconds,
    double BothPlayingSeconds,
    double? TempoDifferenceBPM,
    double? SecondsToMatch,
    double? PhaseErrorMilliseconds,
    double? MeanPhaseErrorMilliseconds)
{
    public bool IsMatched => SecondsToMatch is not null;
    public bool IsLongEnough => BothPlayingSeconds >= PracticeSession.MinimumMixSeconds;
}
