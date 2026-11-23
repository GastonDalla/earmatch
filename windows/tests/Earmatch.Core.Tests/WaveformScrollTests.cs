using Earmatch.Core;

namespace Earmatch.Core.Tests;

sealed class WaveformSession
{
    public const double FrameInterval = 1.0 / 110;
    public const double Refresh = 1.0 / 120;
    public const double PipelineLatency = 0.006;
    public const double Spacing = 30.0;
    public const double PixelsPerPoint = 2.0;
    public const double PixelsPerBeat = Spacing * PixelsPerPoint;

    public readonly record struct Shown(double Time, double Pixels, double Rekordbox);

    private readonly record struct Frame(double Pixels, double Phase);

    private readonly PhaseTracker _tracker = new();
    private readonly PhasePredictor _predictor = new();
    private readonly Queue<(double Available, Frame Frame)> _pending = new();
    private Frame? _frame;
    private double _nextCapture;
    private double _clock;

    public static double RekordboxPixels(double time, Func<double, double> beats)
    {
        const double audioBlock = 512.0 / 44_100;
        var played = beats(Math.Floor(time / audioBlock) * audioBlock);
        return Math.Floor(played * PixelsPerBeat / PixelsPerPoint) * PixelsPerPoint;
    }

    public List<Shown> Run(double end, Func<double, double> beats, Func<double, bool>? captures = null)
    {
        var shown = new List<Shown>();
        while (_clock + Refresh <= end + 1e-9)
        {
            _clock += Refresh;
            while (_nextCapture <= _clock)
            {
                var time = _nextCapture;
                _nextCapture += FrameInterval;
                if (captures is not null && !captures(time)) continue;
                var pixels = RekordboxPixels(time, beats);
                var phase = MeterSession.Wrapped(pixels / PixelsPerBeat);
                var estimate = _tracker.Update(new BeatGridReading(Spacing, [], [], phase), time);
                _predictor.Update(new PhaseAnchor(time, estimate.PhaseInBar, _tracker.BeatsPerSecond, estimate.Status == PhaseTrackerStatus.Live));
                _pending.Enqueue((time + PipelineLatency, new Frame(pixels, phase)));
            }
            while (_pending.TryPeek(out var first) && first.Available <= _clock)
            {
                _frame = first.Frame;
                _pending.Dequeue();
            }
            var target = _clock + Refresh;
            var drawn = _predictor.Phase(target);
            if (_frame is not { } frame) continue;
            var shift = WaveformScroll.Shift(frame.Phase, drawn, Spacing, PixelsPerPoint);
            shown.Add(new Shown(target, frame.Pixels + shift, RekordboxPixels(target, beats)));
        }
        return shown;
    }
}

public class WaveformScrollTests
{
    const double Rate = 2.25;
    const double PixelsPerRefresh = Rate * WaveformSession.PixelsPerBeat * WaveformSession.Refresh;

    static List<double> Steps(List<WaveformSession.Shown> shown) => shown.Skip(1).Zip(shown, (next, previous) => next.Pixels - previous.Pixels).ToList();

    static List<double> Errors(List<WaveformSession.Shown> shown) => shown.Select(refresh => refresh.Pixels - refresh.Rekordbox).ToList();

    static List<WaveformSession.Shown> Playing(Func<double, bool>? captures = null) =>
        new WaveformSession().Run(3.0, time => 1 + Rate * time, captures).Where(refresh => refresh.Time > 0.3).ToList();

    [Fact]
    public void APlayingDeckIsDrawnWhereRekordboxDrawsItWhenTheRefreshReachesTheScreen()
    {
        var errors = Errors(Playing());

        Assert.True(Math.Abs(errors.Average()) <= 0.5, $"mean error {errors.Average()} px");
        Assert.All(errors, error => Assert.True(Math.Abs(error) <= WaveformSession.PixelsPerPoint, $"error {error} px"));
    }

    [Fact]
    public void APlayingDeckScrollsOnAlmostEveryRefreshWithoutSteppingBackOrSkipping()
    {
        var steps = Steps(Playing());

        Assert.All(steps, step => Assert.InRange(step, 0, Math.Ceiling(PixelsPerRefresh)));
        Assert.True(steps.Count(step => step == 0) <= 0.1 * steps.Count, $"{steps.Count(step => step == 0)} of {steps.Count} refreshes held still");
    }

    [Fact]
    public void AMissedCaptureDoesNotMakeTheWaveformJump()
    {
        var shown = Playing(time => (int)Math.Round(time * 110) % 35 != 0);

        Assert.All(Steps(shown), step => Assert.InRange(step, 0, Math.Ceiling(PixelsPerRefresh)));
        Assert.All(Errors(shown), error => Assert.True(Math.Abs(error) <= WaveformSession.PixelsPerPoint, $"error {error} px"));
    }

    [Fact]
    public void APausedDeckStaysWhereRekordboxStopped()
    {
        const double stop = 1.0;
        Func<double, double> beats = time => 1 + Rate * Math.Min(time, stop);
        var shown = new WaveformSession().Run(2.0, beats);
        var stopped = WaveformSession.RekordboxPixels(stop + 0.1, beats);

        Assert.All(shown.Where(refresh => refresh.Time > stop + 0.2), refresh => Assert.Equal(stopped, refresh.Pixels));
    }

    [Fact]
    public void TheShiftFollowsTheDrawnPhaseAcrossTheEndOfTheBar()
    {
        Assert.Equal(2, WaveformScroll.Shift(3.98, 0.02, 30, 2));
        Assert.Equal(-2, WaveformScroll.Shift(0.02, 3.98, 30, 2));
    }

    [Fact]
    public void TheShiftIsRoundedToWholeCapturedPixels()
    {
        Assert.Equal(1, WaveformScroll.Shift(1, 1.01, 30, 2));
        Assert.Equal(0, WaveformScroll.Shift(1, 1.01, 30, 1));
    }

    [Fact]
    public void ASeekTheMeterHasNotFollowedYetIsNotShifted()
    {
        Assert.Equal(0, WaveformScroll.Shift(1, 1 + WaveformScroll.MaxShiftBeats + 0.01, 30, 2));
        Assert.Equal(0, WaveformScroll.Shift(1, 3, 30, 2));
    }

    [Fact]
    public void WithoutAPhaseOrASpacingTheFrameIsShownAsCaptured()
    {
        Assert.Equal(0, WaveformScroll.Shift(null, 1, 30, 2));
        Assert.Equal(0, WaveformScroll.Shift(1, null, 30, 2));
        Assert.Equal(0, WaveformScroll.Shift(1, 1.1, null, 2));
        Assert.Equal(0, WaveformScroll.Shift(1, 1.1, 0, 2));
    }
}
