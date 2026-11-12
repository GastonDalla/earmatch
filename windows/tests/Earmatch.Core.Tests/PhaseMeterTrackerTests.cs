using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class PhaseMeterTrackerTests
{
    const double Frame = 1.0 / 120;

    static BeatGridReading Reading(double phase, double spacing = 30) => new(spacing, [], [], phase);

    static double Play(PhaseTracker tracker, double rate = 2, double spacing = 30)
    {
        var time = 0.0;
        for (var index = 0; index < 60; index++)
        {
            time = index * Frame;
            tracker.Update(Reading((1 + rate * time) % 4, spacing), time);
        }
        return time;
    }

    [Fact]
    public void AGoodReadingIsShownAsIs()
    {
        var estimate = new PhaseTracker().Update(Reading(1.5), 0);

        Assert.Equal(new PhaseEstimate(1.5, 30, PhaseTrackerStatus.Live), estimate);
    }

    [Fact]
    public void AMissedFrameIsHeldAndAdvancedAtTheMeasuredTempo()
    {
        var tracker = new PhaseTracker();
        var last = Play(tracker);
        var lastPhase = 1 + 2 * last;

        var estimate = tracker.Update(null, last + 0.1);

        Assert.Equal(PhaseTrackerStatus.Held, estimate.Status);
        Assert.True(Math.Abs(estimate.PhaseInBar!.Value - (lastPhase + 0.2)) < 0.02);
    }

    [Fact]
    public void AfterAQuarterSecondWithoutAGoodReadingTheMeterShowsNoGrid()
    {
        var tracker = new PhaseTracker();
        var last = Play(tracker);

        var estimate = tracker.Update(null, last + 0.3);

        Assert.Equal(PhaseTrackerStatus.Lost, estimate.Status);
        Assert.Null(estimate.PhaseInBar);
    }

    [Fact]
    public void OneFrameWithAWildSpacingDoesNotMoveTheMeter()
    {
        var tracker = new PhaseTracker();
        var last = Play(tracker);

        var estimate = tracker.Update(Reading(3.6, spacing: 45), last + Frame);

        Assert.Equal(PhaseTrackerStatus.Held, estimate.Status);
        Assert.Equal(30, estimate.BeatSpacing);
        Assert.True(Math.Abs(estimate.PhaseInBar!.Value - (1 + 2 * (last + Frame))) < 0.02);
    }

    [Fact]
    public void ASpacingChangeThatPersistsIsAZoomChange()
    {
        var tracker = new PhaseTracker();
        var time = Play(tracker);
        var estimate = new PhaseEstimate(null, null, PhaseTrackerStatus.Lost);
        for (var index = 0; index < 3; index++)
        {
            time += Frame;
            estimate = tracker.Update(Reading((1 + 2 * time) % 4, spacing: 45), time);
        }

        Assert.Equal(PhaseTrackerStatus.Live, estimate.Status);
        Assert.Equal(45, estimate.BeatSpacing);
    }

    [Fact]
    public void APhaseJumpIsHeldForTwoFramesAndAcceptedOnTheThird()
    {
        var tracker = new PhaseTracker();
        var time = Play(tracker);
        static double Jumped(double time) => (3 + 2 * time) % 4;
        static double Old(double time) => (1 + 2 * time) % 4;

        for (var index = 0; index < 2; index++)
        {
            time += Frame;
            var held = tracker.Update(Reading(Jumped(time)), time);
            Assert.Equal(PhaseTrackerStatus.Held, held.Status);
            Assert.True(Math.Abs(held.PhaseInBar!.Value - Old(time)) < 0.02);
        }

        time += Frame;
        var accepted = tracker.Update(Reading(Jumped(time)), time);
        Assert.Equal(PhaseTrackerStatus.Live, accepted.Status);
        Assert.True(Math.Abs(accepted.PhaseInBar!.Value - Jumped(time)) < 1e-9);
    }

    [Fact]
    public void APausedDeckIsHeldInPlace()
    {
        var tracker = new PhaseTracker();
        var last = Play(tracker, rate: 0);

        var estimate = tracker.Update(null, last + 0.2);

        Assert.Equal(PhaseTrackerStatus.Held, estimate.Status);
        Assert.True(Math.Abs(estimate.PhaseInBar!.Value - 1) < 0.01);
    }

    [Fact]
    public void AFreshReadingAfterTheGridWasLostIsShownAtOnce()
    {
        var tracker = new PhaseTracker();
        var last = Play(tracker);
        tracker.Update(null, last + 0.5);

        var estimate = tracker.Update(Reading(0.5, spacing: 60), last + 0.6);

        Assert.Equal(new PhaseEstimate(0.5, 60, PhaseTrackerStatus.Live), estimate);
    }

    static double Feed(PhaseTracker tracker, double start, double end, Func<double, double> phase, double spacing = 30)
    {
        const double step = 1.0 / 110;
        var time = start;
        var last = start;
        while (time <= end + 1e-9)
        {
            tracker.Update(Reading(MeterSession.Quantized(phase(time)), spacing), time);
            last = time;
            time += step;
        }
        return last;
    }

    [Fact]
    public void TheTempoIsMeasuredDespiteThePixelRounding()
    {
        var tracker = new PhaseTracker();
        Feed(tracker, 0, 0.5, time => 1 + 2.25 * time);

        Assert.True(Math.Abs(tracker.BeatsPerSecond!.Value - 2.25) < 0.05);
    }

    [Fact]
    public void OneSlightlyWrongReadingBarelyMovesTheTempo()
    {
        var tracker = new PhaseTracker();
        var last = Feed(tracker, 0, 0.5, time => 1 + 2.25 * time);
        var wrong = last + 1.0 / 110;
        tracker.Update(Reading(MeterSession.Quantized(1 + 2.25 * wrong + 0.15)), wrong);
        var next = wrong + 1.0 / 110;
        tracker.Update(Reading(MeterSession.Quantized(1 + 2.25 * next)), next);

        Assert.True(Math.Abs(tracker.BeatsPerSecond!.Value - 2.25) < 0.05);
    }

    [Fact]
    public void APausedDeckMeasuresZeroWithinFourFrames()
    {
        var tracker = new PhaseTracker();
        var last = Feed(tracker, 0, 0.5, time => 1 + 2.25 * time);
        Feed(tracker, last + 1.0 / 110, last + 4.0 / 110, _ => 1 + 2.25 * 0.5);

        Assert.Equal(0, tracker.BeatsPerSecond);
    }

    [Fact]
    public void AResumedDeckMeasuresItsTempoAgainQuicklyWithoutOvershooting()
    {
        var tracker = new PhaseTracker();
        const double step = 1.0 / 110;
        var last = Feed(tracker, 0, 0.3, _ => 2);
        Assert.Equal(0, tracker.BeatsPerSecond);

        var time = last;
        var rates = new List<double>();
        while (time < last + 0.1)
        {
            time += step;
            tracker.Update(Reading(MeterSession.Quantized(2 + 2.25 * (time - last))), time);
            rates.Add(tracker.BeatsPerSecond ?? 0);
        }
        Assert.All(rates, rate => Assert.True(rate <= 2.25 * 1.15, $"{rate}"));
        Assert.True(Math.Abs(rates[^1] - 2.25) < 0.1);
        Assert.True(Math.Abs(rates[5] - 2.25) < 2.25 * 0.3);
    }

    [Fact]
    public void ASeekKeepsTheTempo()
    {
        var tracker = new PhaseTracker();
        var time = Play(tracker);
        for (var index = 0; index < 3; index++)
        {
            time += Frame;
            tracker.Update(Reading((3 + 2 * time) % 4), time);
        }

        Assert.True(Math.Abs(tracker.BeatsPerSecond!.Value - 2) < 0.05);
    }

    [Fact]
    public void AfterTheGridWasLostTheTempoIsUnknownUntilMeasuredAgain()
    {
        var tracker = new PhaseTracker();
        var last = Play(tracker);
        tracker.Update(null, last + 0.5);

        tracker.Update(Reading(0.5), last + 0.6);

        Assert.Null(tracker.BeatsPerSecond);
    }

    [Fact]
    public void PhysicalPixelSpacingsKeepThePointTolerances()
    {
        static double? StoppedRate(PhaseTracker tracker, double spacing)
        {
            var last = Feed(tracker, 0, 0.5, time => 1 + 2.25 * time, spacing);
            for (var index = 1; index <= 4; index++)
            {
                tracker.Update(Reading(2.125 + index % 2 * 0.006, spacing), last + index / 110.0);
            }
            return tracker.BeatsPerSecond;
        }

        Assert.Equal(0, StoppedRate(new PhaseTracker(), spacing: 30));
        Assert.Equal(0, StoppedRate(new PhaseTracker(layoutScale: 2), spacing: 60));
    }
}
