using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class PracticeSessionTests
{
    internal static readonly DateTimeOffset Origin = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(800_000_000);
    const double BeatMilliseconds = 60_000 / 128.0;

    static DeckScript Master(Motion motion) => new(1, motion) { Master = _ => true };

    static PracticeResult Only(List<PracticeResult> results)
    {
        Assert.Single(results);
        return results[0];
    }

    static void Near(double expected, double? actual, double tolerance)
    {
        Assert.True(actual is { } value && Math.Abs(value - expected) < tolerance, $"expected {expected} ± {tolerance}, got {actual}");
    }

    [Fact]
    public void AMixReportsWhenTheTemposMatchedAndThePhaseErrorFromThenOn()
    {
        var incoming = Motion.Approach(from: 126, to: 128, at: 5, offset: 0.02);
        var result = Only(new MixScript(20, [Master(Motion.Steady(128)), new DeckScript(2, incoming)]).Run());

        var mix = result.Mix;
        Assert.Equal(1, mix.OutgoingDeck);
        Assert.Equal(2, mix.IncomingDeck);
        Assert.Equal(Origin, mix.StartedAt);
        Near(20, (mix.EndedAt - Origin).TotalSeconds, 0.1);
        Near(20, mix.BothPlayingSeconds, 0.1);
        Assert.True(mix.SecondsToMatch is >= 4.5 and <= 6.5, $"{mix.SecondsToMatch}");
        const double expected = 0.02 * BeatMilliseconds;
        Near(expected, mix.MeanPhaseErrorMilliseconds, 0.5);
        Near(expected, mix.WorstPhaseErrorMilliseconds, 0.5);
        Near(0, mix.TempoDriftBPM, 0.05);
    }

    [Fact]
    public void TheWorstPhaseErrorIsTheLargestAbsoluteErrorAfterTheMatch()
    {
        var nudge = Motion.Steady(128, offset: 0.01).WithOffset(time => time is >= 10 and < 11 ? -0.05 : 0.01);
        var mix = Only(new MixScript(16, [Master(Motion.Steady(128)), new DeckScript(2, nudge)]).Run()).Mix;

        Near(0.05 * BeatMilliseconds, mix.WorstPhaseErrorMilliseconds, 0.5);
        Assert.True(mix.MeanPhaseErrorMilliseconds < 0.02 * BeatMilliseconds, $"{mix.MeanPhaseErrorMilliseconds}");
    }

    [Theory]
    [InlineData(0.98, -0.02)]
    [InlineData(0.52, -0.48)]
    [InlineData(3.7, -0.3)]
    [InlineData(-0.2, -0.2)]
    [InlineData(2.25, 0.25)]
    public void ThePhaseErrorWrapsToTheNearestBeat(double offset, double wrapped)
    {
        var result = Only(new MixScript(12, [Master(Motion.Steady(128)), new DeckScript(2, Motion.Steady(128, offset))]).Run());

        Near(Math.Abs(wrapped) * BeatMilliseconds, result.Mix.MeanPhaseErrorMilliseconds, 0.5);
        Near(wrapped * BeatMilliseconds, result.Trace.Samples[^1].ErrorMilliseconds, 0.5);
    }

    [Fact]
    public void ThePhaseErrorUsesTheSlaveDecksBeatLength()
    {
        var result = Only(new MixScript(12, [
            new DeckScript(1, Motion.Steady(120, offset: 0.1)),
            new DeckScript(2, Motion.Steady(128)) { Master = _ => true },
        ]).Run());

        Assert.Equal(2, result.Mix.OutgoingDeck);
        Assert.Equal(1, result.Mix.IncomingDeck);
        var first = result.Trace.Samples[0];
        Assert.Equal(0, first.Seconds);
        Near(50, first.ErrorMilliseconds, 0.01);
    }

    [Fact]
    public void MatchedTemposFromTheStartMatchAtOnce()
    {
        var result = Only(new MixScript(12, [Master(Motion.Steady(128)), new DeckScript(2, Motion.Steady(128, offset: 0.01))]).Run());

        Assert.True(result.Mix.SecondsToMatch < 0.5, $"{result.Mix.SecondsToMatch}");
    }

    [Fact]
    public void TemposThatNeverMatchLeaveTheMatchTimeAndTheErrorEmpty()
    {
        var mix = Only(new MixScript(12, [Master(Motion.Steady(128)), new DeckScript(2, Motion.Steady(125))]).Run()).Mix;

        Assert.Null(mix.SecondsToMatch);
        Assert.Null(mix.MeanPhaseErrorMilliseconds);
        Assert.Null(mix.WorstPhaseErrorMilliseconds);
        Assert.Null(mix.TempoDriftBPM);
    }

    [Fact]
    public void NoisyMeasuredTempoStillMatchesWhenThePhasesAdvanceTogether()
    {
        var jitter = new SplitMix(seed: 7);
        var noisy = Motion.Steady(128, offset: 0.01).WithBpm(_ => 128 + jitter.Next(-1.5, 1.5));
        var result = Only(new MixScript(14, [Master(Motion.Steady(128)), new DeckScript(2, noisy)]).Run());

        Assert.True(result.Mix.SecondsToMatch < 0.5, $"{result.Mix.SecondsToMatch}");
    }

    [Fact]
    public void TempoDriftIsTheMeanTempoDifferenceOnceMatched()
    {
        var result = Only(new MixScript(30, [Master(Motion.Steady(128)), new DeckScript(2, Motion.Steady(128.05))]).Run());

        Near(0.05, result.Mix.TempoDriftBPM, 0.02);
    }

    [Fact]
    public void EmptySnapshotsAreAGapNotAnErrorAndNotTheEndOfTheMix()
    {
        var mix = Only(new MixScript(20, [Master(Motion.Steady(128)), new DeckScript(2, Motion.Steady(128, offset: 0.02))])
        {
            IsEmpty = time => time is >= 8 and < 11,
        }.Run()).Mix;

        Near(17, mix.BothPlayingSeconds, 0.2);
        Near(20, (mix.EndedAt - Origin).TotalSeconds, 0.1);
        Near(0.02 * BeatMilliseconds, mix.WorstPhaseErrorMilliseconds, 0.5);
    }

    [Fact]
    public void APhaseThatCannotBeReadIsNotCountedAsError()
    {
        var mix = Only(new MixScript(16, [
            Master(Motion.Steady(128)),
            new DeckScript(2, Motion.Steady(128, offset: 0.02).WithOffset(time => time is >= 8 and < 10 ? 0.4 : 0.02))
            {
                PhaseKnown = time => time is not (>= 8 and < 10),
            },
        ]).Run()).Mix;

        Near(0.02 * BeatMilliseconds, mix.WorstPhaseErrorMilliseconds, 0.5);
        Near(16, mix.BothPlayingSeconds, 0.2);
    }

    [Fact]
    public void ASyncMasterSwapKeepsOneMixWithTheSameOrientation()
    {
        var result = Only(new MixScript(20, [
            new DeckScript(1, Motion.Steady(128)) { Master = time => time < 10 },
            new DeckScript(2, Motion.Steady(128, offset: 0.02)) { Master = time => time >= 10 },
        ]).Run());

        Assert.Equal(1, result.Mix.OutgoingDeck);
        Assert.Equal(2, result.Mix.IncomingDeck);
        Near(0.02 * BeatMilliseconds, result.Mix.MeanPhaseErrorMilliseconds, 0.5);
        Assert.All(result.Trace.Samples, sample => Assert.True(sample.ErrorMilliseconds > 0, $"{sample}"));
    }

    [Fact]
    public void APauseShorterThanASecondDoesNotEndTheMix()
    {
        var mix = Only(new MixScript(20, [
            Master(Motion.Steady(128)),
            new DeckScript(2, Motion.Steady(128, offset: 0.02)) { Playing = time => time is not (>= 9 and < 9.6) },
        ]).Run()).Mix;

        Near(19.4, mix.BothPlayingSeconds, 0.2);
    }

    [Fact]
    public void APauseOfASecondOrMoreEndsTheMix()
    {
        var results = new MixScript(25, [
            Master(Motion.Steady(128)),
            new DeckScript(2, Motion.Steady(128, offset: 0.02)) { Playing = time => time is not (>= 10 and < 11.5) },
        ]).Run();

        Assert.Equal(2, results.Count);
        Assert.Equal([10.0, 25.0], results.Select(result => Math.Round((result.Mix.EndedAt - Origin).TotalSeconds, MidpointRounding.AwayFromZero)));
    }

    [Fact]
    public void AMixShorterThanEightSecondsIsNotRecorded()
    {
        var results = new MixScript(12, [
            Master(Motion.Steady(128)),
            new DeckScript(2, Motion.Steady(128)) { Playing = time => time is >= 2 and < 9.5 },
        ]).Run();

        Assert.Empty(results);
    }

    [Fact]
    public void ALongGapClosesTheOpenMixAtTheLastTimeBothDecksPlayed()
    {
        var session = new PracticeSession(Origin);
        new MixScript(12, [Master(Motion.Steady(128)), new DeckScript(2, Motion.Steady(128))]) { Finishes = false }.Run(session);
        Assert.Null(session.Ingest(LiveDecksSnapshot.Empty, 12.05));
        Assert.Null(session.Advance(12 + PracticeSession.GapTolerance - 0.5));

        var closed = session.Advance(12 + PracticeSession.GapTolerance + 0.5);

        Assert.NotNull(closed);
        Near(12, (closed.Mix.EndedAt - Origin).TotalSeconds, 0.1);
        Assert.Null(session.Live);
        Assert.False(session.IsMixing);
    }

    [Fact]
    public void LiveStatusFollowsTheOpenMix()
    {
        var session = new PracticeSession(Origin);
        Assert.Null(session.Live);
        new MixScript(6, [Master(Motion.Steady(128)), new DeckScript(2, Motion.Steady(128, offset: 0.02))]) { Finishes = false }.Run(session);

        var live = session.Live;
        Assert.NotNull(live);
        Assert.True(session.IsMixing);
        Assert.Equal(1, live.OutgoingDeck);
        Assert.Equal(2, live.IncomingDeck);
        Near(6, live.ElapsedSeconds, 0.1);
        Near(0, live.TempoDifferenceBPM, 0.1);
        Assert.NotNull(live.SecondsToMatch);
        Near(0.02 * BeatMilliseconds, live.PhaseErrorMilliseconds, 0.5);
        Assert.False(live.IsLongEnough);
    }

    [Fact]
    public void TheTraceSamplesThePhaseErrorAboutTenTimesASecond()
    {
        var result = Only(new MixScript(20, [Master(Motion.Steady(128)), new DeckScript(2, Motion.Approach(from: 127, to: 128, at: 4, offset: 0.02))]).Run());

        var trace = result.Trace;
        Assert.Equal(result.Mix.Id, trace.MixID);
        Assert.InRange(trace.Samples.Count, 190, 205);
        Near(20, trace.DurationSeconds, 0.1);
        Assert.Equal(result.Mix.SecondsToMatch, trace.MatchedAtSeconds);
        Assert.Equal(trace.Samples.Select(sample => sample.Seconds).Order(), trace.Samples.Select(sample => sample.Seconds));
    }

    [Fact]
    public void TheReplayReadsTheErrorBetweenSamplesAndFindsTheWorstMatchedMoment()
    {
        var trace = new PracticeTrace(Guid.NewGuid(), 3, 1,
        [
            new PracticeTraceSample(0, -90), new PracticeTraceSample(1, 4), new PracticeTraceSample(2, -12), new PracticeTraceSample(3, 6),
        ]);

        Assert.Equal(-90, trace.ErrorMilliseconds(-1));
        Assert.Equal(-4, trace.ErrorMilliseconds(1.5));
        Assert.Equal(-12, trace.ErrorMilliseconds(2));
        Assert.Equal(6, trace.ErrorMilliseconds(9));
        Assert.Equal(2, trace.WorstSample?.Seconds);
        Assert.Null((trace with { MatchedAtSeconds = null }).WorstSample);
    }

    [Fact]
    public void AMixIsSavedOnceBothDecksPlayedEightSecondsTogether()
    {
        Assert.Equal(8, PracticeSession.MinimumMixSeconds);
        var live = new PracticeLiveStatus(1, 2, 9, PracticeSession.MinimumMixSeconds - 0.1, null, null, null, null);
        Assert.False(live.IsLongEnough);
        Assert.True((live with { BothPlayingSeconds = PracticeSession.MinimumMixSeconds }).IsLongEnough);
    }

    [Fact]
    public void ResultsSurviveTheHistoryFileRoundTrip()
    {
        var result = Only(new MixScript(12, [Master(Motion.Steady(128)), new DeckScript(2, Motion.Steady(128, offset: 0.03))]).Run());
        var history = new PracticeHistory();
        history.Record(result);
        var directory = Directory.CreateTempSubdirectory("earmatch-practice-session-");
        try
        {
            var file = new PracticeHistoryFile(Path.Combine(directory.FullName, "practice.json"));
            file.Save(history);

            var loaded = file.Load().History;

            Assert.Equal([result.Mix], loaded.Mixes);
            Assert.Equal(result.Trace, loaded.LastTrace);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}

/// A deck's position in beats and the tempo it reports, over time.
sealed record Motion(Func<double, double> Beats, Func<double, double> Bpm)
{
    public static Motion Steady(double bpm, double offset = 0) => new(time => bpm / 60 * time + offset, _ => bpm);

    public static Motion Approach(double from, double to, double at, double offset) => new(
        time => to / 60 * time + offset - (to - from) / 60 * Math.Min(time - at, 0),
        time => time < at ? from : to);

    public Motion WithOffset(Func<double, double> offset)
    {
        var bpm = Bpm;
        return new Motion(time => bpm(time) / 60 * time + offset(time), bpm);
    }

    public Motion WithBpm(Func<double, double> reported) => this with { Bpm = reported };
}

sealed record DeckScript(int Number, Motion Motion)
{
    public Func<double, bool> Master { get; init; } = _ => false;
    public Func<double, bool> Playing { get; init; } = _ => true;
    public Func<double, bool> PhaseKnown { get; init; } = _ => true;
}

/// Feeds a session the decks' snapshots at 30 per second, then closes it with a long gap.
sealed record MixScript(double Duration, IReadOnlyList<DeckScript> Decks)
{
    public double Rate { get; init; } = 30;
    public Func<double, bool> IsEmpty { get; init; } = _ => false;
    public bool Finishes { get; init; } = true;

    public List<PracticeResult> Run() => Run(new PracticeSession(PracticeSessionTests.Origin));

    public List<PracticeResult> Run(PracticeSession session)
    {
        var results = new List<PracticeResult>();
        var stoppedAt = new Dictionary<int, double>();
        var steps = (int)Math.Round(Duration * Rate, MidpointRounding.AwayFromZero);
        for (var step = 0; step <= steps; step++)
        {
            var time = step / Rate;
            if (IsEmpty(time))
            {
                if (session.Ingest(LiveDecksSnapshot.Empty, time) is { } gapResult) results.Add(gapResult);
                continue;
            }
            var decks = Decks.Select(deck =>
            {
                var playing = deck.Playing(time);
                if (playing) stoppedAt.Remove(deck.Number);
                else stoppedAt.TryAdd(deck.Number, time);
                var position = deck.Motion.Beats(stoppedAt.TryGetValue(deck.Number, out var stopped) ? stopped : time);
                var phase = (position % 4 + 4) % 4;
                return new LiveDeckSnapshot(deck.Number, null, deck.Master(time), null,
                    PhaseInBar: deck.PhaseKnown(time) ? phase : null,
                    BeatsPerSecond: playing ? deck.Motion.Bpm(time) / 60 : 0);
            }).ToValueList();
            if (session.Ingest(new LiveDecksSnapshot(decks, 60, 20), time) is { } result) results.Add(result);
        }
        if (Finishes && session.Advance(Duration + PracticeSession.GapTolerance + 1) is { } last) results.Add(last);
        return results;
    }
}

/// The SplitMix64 generator, for reproducible jitter.
sealed class SplitMix(ulong seed)
{
    private ulong _state = seed;

    public double Next(double lower, double upper)
    {
        unchecked
        {
            _state += 0x9E37_79B9_7F4A_7C15;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58_476D_1CE4_E5B9;
            z = (z ^ (z >> 27)) * 0x94D0_49BB_1331_11EB;
            z ^= z >> 31;
            var unit = (double)(z >> 11) / (1UL << 53);
            return lower + unit * (upper - lower);
        }
    }
}
