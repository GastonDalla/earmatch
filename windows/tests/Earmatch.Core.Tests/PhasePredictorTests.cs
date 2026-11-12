using Earmatch.Core;

namespace Earmatch.Core.Tests;

sealed class MeterSession
{
    public const double FrameInterval = 1.0 / 110;
    public const double Refresh = 1.0 / 120;
    public const double PipelineLatency = 0.006;
    public const double Spacing = 30.0;

    public readonly record struct Drawn(double Time, double? Shown, double? Truth);

    public PhaseTracker Tracker { get; } = new();
    public PhasePredictor Predictor { get; } = new();
    private double _nextFrame;
    private double _clock;

    public List<Drawn> Run(double end, Func<double, double?> truth, Func<double, bool>? captures = null)
    {
        var drawn = new List<Drawn>();
        while (_clock + Refresh <= end + 1e-9)
        {
            _clock += Refresh;
            while (_nextFrame <= _clock - PipelineLatency)
            {
                var time = _nextFrame;
                _nextFrame += FrameInterval;
                if (captures is not null && !captures(time)) continue;
                var reading = truth(time) is { } phase ? new BeatGridReading(Spacing, [], [], Quantized(phase)) : null;
                var estimate = Tracker.Update(reading, time);
                Predictor.Update(new PhaseAnchor(time, estimate.PhaseInBar, Tracker.BeatsPerSecond, estimate.Status == PhaseTrackerStatus.Live));
            }
            var target = _clock + Refresh;
            drawn.Add(new Drawn(target, Predictor.Phase(target), truth(target)));
        }
        return drawn;
    }

    public static double Wrapped(double beats)
    {
        var rest = beats % 4;
        return rest < 0 ? rest + 4 : rest;
    }

    public static double Difference(double a, double b) => Wrapped(a - b + 2) - 2;

    public static double Quantized(double phase)
    {
        const double quantum = 0.5 / Spacing;
        return Wrapped(Math.Round(phase / quantum, MidpointRounding.AwayFromZero) * quantum);
    }
}

public class PhasePredictorTests
{
    const double Rate = 2.25;
    const double ExpectedStep = Rate * MeterSession.Refresh;

    static double Wrapped(double beats) => MeterSession.Wrapped(beats);

    static double Difference(double a, double b) => MeterSession.Difference(a, b);

    static List<double> Steps(IEnumerable<MeterSession.Drawn> drawn)
    {
        var shown = drawn.Select(frame => frame.Shown ?? throw new Xunit.Sdk.XunitException($"nothing drawn at {frame.Time}")).ToList();
        return shown.Skip(1).Zip(shown, Difference).ToList();
    }

    static void Near(double? shown, double? truth, double tolerance, double time)
    {
        Assert.True(shown is not null && truth is not null, $"missing value at {time}");
        Assert.True(Math.Abs(Difference(shown.Value, truth.Value)) < tolerance, $"{shown} vs {truth} at {time}");
    }

    static bool IsSteady(double step) => step > 0.5 * ExpectedStep && step < 1.5 * ExpectedStep;

    [Fact]
    public void ASteadyTempoIsDrawnWhereRekordboxIsWhenTheRefreshReachesTheScreen()
    {
        var drawn = new MeterSession().Run(1.0, time => Wrapped(1 + Rate * time));

        var settled = drawn.Where(frame => frame.Time > 0.2).ToList();
        foreach (var frame in settled) Near(frame.Shown, frame.Truth, 0.02, frame.Time);
        Assert.All(Steps(settled), step => Assert.True(IsSteady(step), $"{step}"));
    }

    [Fact]
    public void APausedDeckStopsWhereRekordboxStopped()
    {
        const double paused = 1 + Rate * 0.5;
        var drawn = new MeterSession().Run(1.0, time => Wrapped(1 + Rate * Math.Min(time, 0.5)));

        foreach (var frame in drawn.Where(frame => frame.Time > 0.6)) Near(frame.Shown, paused, 0.01, frame.Time);
        foreach (var frame in drawn)
        {
            Assert.True(frame.Shown is not null, $"nothing drawn at {frame.Time}");
            Assert.True(Difference(frame.Shown.Value, frame.Truth!.Value) < 0.15, $"{frame.Shown} vs {frame.Truth} at {frame.Time}");
        }
        Assert.All(Steps(drawn.Where(frame => frame.Time > 0.2 && frame.Time < 0.5)), step => Assert.True(step > 0, $"{step}"));
    }

    [Fact]
    public void AResumedDeckCatchesUpWithinAFewFramesAndNeverStepsBack()
    {
        var drawn = new MeterSession().Run(1.0, time => time < 0.4 ? 2 : Wrapped(2 + Rate * (time - 0.4)));

        foreach (var frame in drawn.Where(frame => frame.Time > 0.5)) Near(frame.Shown, frame.Truth, 0.03, frame.Time);
        Assert.All(Steps(drawn), step => Assert.True(step >= 0, $"{step}"));
    }

    [Fact]
    public void ASeekIsShownOnceTheTrackerAcceptsIt()
    {
        var drawn = new MeterSession().Run(1.0, time => Wrapped(1 + Rate * time + (time < 0.5 ? 0 : 1.5)));

        var beforeAccept = drawn.Where(frame => frame.Time > 0.2 && frame.Time < 0.53).ToList();
        foreach (var frame in beforeAccept) Near(frame.Shown, Wrapped(1 + Rate * frame.Time), 0.02, frame.Time);
        Assert.All(Steps(beforeAccept), step => Assert.True(IsSteady(step), $"{step}"));
        foreach (var frame in drawn.Where(frame => frame.Time > 0.56)) Near(frame.Shown, frame.Truth, 0.03, frame.Time);
    }

    [Fact]
    public void ABackwardSeekIsShownToo()
    {
        var drawn = new MeterSession().Run(1.0, time => Wrapped(1 + Rate * time - (time < 0.5 ? 0 : 1.0)));

        foreach (var frame in drawn.Where(frame => frame.Time > 0.56)) Near(frame.Shown, frame.Truth, 0.03, frame.Time);
    }

    [Fact]
    public void TheMeterWrapsPastTheFourthBeatWithoutSteppingBack()
    {
        var drawn = new MeterSession().Run(0.5, time => Wrapped(3.5 + Rate * time));
        var shown = drawn.Select(frame => frame.Shown!.Value).ToList();

        Assert.All(shown, phase => Assert.True(phase >= 0 && phase < 4, $"{phase}"));
        Assert.Contains(shown, phase => phase > 3.9);
        Assert.Contains(shown, phase => phase < 0.1);
        Assert.All(Steps(drawn), step => Assert.True(step >= 0, $"{step}"));
        Assert.All(Steps(drawn.Where(frame => frame.Time > 0.15)), step => Assert.True(IsSteady(step), $"{step}"));
        foreach (var frame in drawn.Where(frame => frame.Time > 0.2)) Near(frame.Shown, frame.Truth, 0.02, frame.Time);
    }

    [Fact]
    public void AHeldReadingKeepsAdvancingAtTheMeasuredTempo()
    {
        var drawn = new MeterSession().Run(1.0, time => time is >= 0.5 and <= 0.6 ? null : Wrapped(1 + Rate * time));

        var held = drawn.Where(frame => frame.Time > 0.45 && frame.Time < 0.75).ToList();
        Assert.All(Steps(held), step => Assert.True(IsSteady(step), $"{step}"));
        foreach (var frame in held) Near(frame.Shown, Wrapped(1 + Rate * frame.Time), 0.03, frame.Time);
    }

    [Fact]
    public void AShortCaptureGapKeepsTheMeterMovingAtTheMeasuredTempo()
    {
        var drawn = new MeterSession().Run(1.0, time => Wrapped(1 + Rate * time), captures: time => time is not (>= 0.5 and <= 0.65));

        var around = drawn.Where(frame => frame.Time > 0.45 && frame.Time < 0.8).ToList();
        Assert.All(Steps(around), step => Assert.True(IsSteady(step), $"{step}"));
        foreach (var frame in around) Near(frame.Shown, frame.Truth, 0.03, frame.Time);
    }

    [Fact]
    public void ALongCaptureGapShowsNoGridUntilFramesReturn()
    {
        var drawn = new MeterSession().Run(1.2, time => Wrapped(1 + Rate * time), captures: time => time is not (>= 0.5 and <= 0.9));

        Assert.All(drawn.Where(frame => frame.Time > 0.5 && frame.Time < 0.75), frame => Assert.NotNull(frame.Shown));
        Assert.All(drawn.Where(frame => frame.Time > 0.83 && frame.Time < 0.9), frame => Assert.Null(frame.Shown));
        foreach (var frame in drawn.Where(frame => frame.Time > 1.0)) Near(frame.Shown, frame.Truth, 0.03, frame.Time);
    }

    [Fact]
    public void ALostGridShowsNothing()
    {
        var predictor = new PhasePredictor();
        predictor.Update(new PhaseAnchor(0, 1, 2, IsMeasured: true));
        predictor.Update(new PhaseAnchor(0.01, null, null, IsMeasured: false));

        Assert.Null(predictor.Phase(0.02));
    }
}
