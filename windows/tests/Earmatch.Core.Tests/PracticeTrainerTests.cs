using Earmatch.Core;

namespace Earmatch.Core.Tests;

/// The mix trainer fed from the live decks (PracticeController.swift): the transition being timed is shown at most 15
/// times a second, at once when it starts, ends or its tempos match, and a finished mix is handed over to be saved.
public class PracticeTrainerTests
{
    static LiveDecksSnapshot Playing(double time, double bpm2 = 128) => new(
    [
        new LiveDeckSnapshot(1, "A", IsMaster: true, null, PhaseInBar: 128 / 60.0 * time % 4, BeatsPerSecond: 128 / 60.0),
        new LiveDeckSnapshot(2, "B", IsMaster: false, null, PhaseInBar: bpm2 / 60.0 * time % 4, BeatsPerSecond: bpm2 / 60.0),
    ], 60, 20);

    [Fact]
    public void AMixShowsAtOnceAndThenAtMostFifteenTimesASecond()
    {
        var trainer = new PracticeTrainer(PracticeSessionTests.Origin);
        var published = 0;

        for (var step = 0; step <= 60; step++)
        {
            var time = step / 60.0;
            if (trainer.Ingest(Playing(time), time, PracticeSessionTests.Origin).LiveChanged) published++;
            if (step == 0) Assert.NotNull(trainer.Live);
        }

        Assert.True(trainer.IsMixing);
        Assert.InRange(published, 2, 16);
    }

    [Fact]
    public void AFinishedMixIsHandedOverOnceAndTheLiveStatusClears()
    {
        var trainer = new PracticeTrainer(PracticeSessionTests.Origin);
        var results = new List<PracticeResult>();
        for (var step = 0; step <= 12 * 30; step++)
        {
            var time = step / 30.0;
            if (trainer.Ingest(Playing(time), time, PracticeSessionTests.Origin).Result is { } result) results.Add(result);
        }

        var (last, changed) = trainer.Advance(12 + PracticeSession.GapTolerance + 1, PracticeSessionTests.Origin);

        Assert.Empty(results);
        Assert.NotNull(last);
        Assert.True(changed);
        Assert.Null(trainer.Live);
        Assert.False(trainer.IsMixing);
    }

    [Fact]
    public void AGapCheckIsNeededOnlyWhileAMixIsOpenAndNothingIsRead()
    {
        var trainer = new PracticeTrainer(PracticeSessionTests.Origin);

        Assert.False(trainer.Ingest(LiveDecksSnapshot.Empty, 0, PracticeSessionTests.Origin).NeedsGapCheck);
        trainer.Ingest(Playing(0.1), 0.1, PracticeSessionTests.Origin);
        Assert.False(trainer.Ingest(Playing(0.2), 0.2, PracticeSessionTests.Origin).NeedsGapCheck);
        Assert.True(trainer.Ingest(LiveDecksSnapshot.Empty, 0.3, PracticeSessionTests.Origin).NeedsGapCheck);
    }
}
