namespace Earmatch.Core;

public enum PhaseTrackerStatus
{
    /// The frame's reading is shown.
    Live,
    /// The reading was missing or doubtful: the last good one, moved on at the measured tempo.
    Held,
    /// No good reading for a while: the meter shows no grid.
    Lost,
}

public readonly record struct PhaseEstimate(double? PhaseInBar, double? BeatSpacing, PhaseTrackerStatus Status);

/// Smooths a deck's beat grid readings frame by frame: a missed or wild frame is held at the measured tempo, a jump
/// (a seek, a zoom change) is believed once three frames agree on it, and the tempo is measured from the phases.
public sealed class PhaseTracker
{
    public const double HoldDuration = 0.25;
    public const double SpacingTolerance = 0.15;
    internal const double PhaseTolerance = 0.25;
    internal const int FramesToConfirm = 3;

    private readonly record struct Sample(double Time, double Phase, double Spacing);

    private readonly double _layoutScale;
    private readonly BeatRateEstimator _rate = new();
    private Sample? _good;
    private (Sample Sample, int Count)? _candidate;

    /// The layout scale is the screen units per point of the readings' beat spacing (PixelImage.LayoutScale): the tempo
    /// measurement tolerates a quarter of a point of marker movement, as on macOS.
    public PhaseTracker(double layoutScale = 1)
    {
        _layoutScale = layoutScale;
    }

    public double? BeatsPerSecond => _rate.BeatsPerSecond;

    public PhaseEstimate Update(BeatGridReading? reading, double time)
    {
        if (reading?.PhaseInBar is not { } phase) return Hold(time);
        var sample = new Sample(time, phase, reading.BeatSpacing);
        if (_good is not { } good || time - good.Time > HoldDuration)
        {
            _candidate = null;
            _rate.Reset();
            return Accept(sample);
        }
        if (Agrees(sample, good))
        {
            _candidate = null;
            return Accept(sample);
        }
        if (_candidate is { } pending && Agrees(sample, pending.Sample))
        {
            if (pending.Count + 1 >= FramesToConfirm)
            {
                _candidate = null;
                _rate.Restart();
                return Accept(sample);
            }
            _candidate = (sample, pending.Count + 1);
        }
        else
        {
            _candidate = (sample, 1);
        }
        return Hold(time);
    }

    private PhaseEstimate Accept(Sample sample)
    {
        _rate.Add(sample.Time, sample.Phase, quantum: 1 / Math.Max(sample.Spacing / _layoutScale, 1));
        _good = sample;
        return new PhaseEstimate(sample.Phase, sample.Spacing, PhaseTrackerStatus.Live);
    }

    private PhaseEstimate Hold(double time)
    {
        if (_good is not { } good || time - good.Time > HoldDuration) return new PhaseEstimate(null, null, PhaseTrackerStatus.Lost);
        return new PhaseEstimate(PredictedPhase(good, time), good.Spacing, PhaseTrackerStatus.Held);
    }

    private bool Agrees(Sample sample, Sample reference) =>
        Math.Abs(sample.Spacing / reference.Spacing - 1) <= SpacingTolerance
        && Math.Abs(CircularBeats(sample.Phase - PredictedPhase(reference, sample.Time))) <= PhaseTolerance;

    private double PredictedPhase(Sample sample, double time) =>
        Numbers.PositiveModulo(sample.Phase + (BeatsPerSecond ?? 0) * (time - sample.Time), BeatGridDetector.BeatsPerBar);

    /// A phase difference wrapped to the nearest equivalent, from -2 to 2 beats.
    internal static double CircularBeats(double difference) => Numbers.PositiveModulo(difference + 2, BeatGridDetector.BeatsPerBar) - 2;
}

/// Measures a deck's tempo in beats per second from the phases of the last 150 ms: the median slope between pairs of
/// readings, so one pixel-rounded or wrong reading barely moves it, and zero as soon as the phase stops moving.
internal sealed class BeatRateEstimator
{
    internal const double Window = 0.15;
    internal const double MinimumPairSpan = 0.02;
    internal const double StillSpanMinimum = 0.015;
    internal const double StillSpanMaximum = 0.1;

    private readonly List<(double Time, double Beats)> _samples = new(32);
    private readonly List<double> _slopes = new(512);

    public double? BeatsPerSecond { get; private set; }

    public void Reset()
    {
        _samples.Clear();
        BeatsPerSecond = null;
    }

    public void Restart() => _samples.Clear();

    public void Add(double time, double phase, double quantum)
    {
        (double Time, double Beats)? previous = _samples.Count > 0 ? _samples[^1] : null;
        if (previous is { } before && time <= before.Time) return;
        var beats = previous is { } last ? last.Beats + PhaseTracker.CircularBeats(phase - last.Beats) : phase;
        var tolerance = quantum / 4;
        _samples.Add((time, beats));
        var firstKept = _samples.FindIndex(sample => time - sample.Time <= Window);
        if (firstKept > 0) _samples.RemoveRange(0, firstKept);

        if (BeatsPerSecond == 0 && previous is { } stopped)
        {
            if (!(Math.Abs(beats - stopped.Beats) > tolerance)) return;
            _samples.Clear();
            _samples.Add(stopped);
            _samples.Add((time, beats));
            BeatsPerSecond = (beats - stopped.Beats) / (time - stopped.Time);
            return;
        }
        if (StillSince(tolerance, quantum) is { } stillSince)
        {
            BeatsPerSecond = 0;
            _samples.RemoveRange(0, stillSince);
            return;
        }
        BeatsPerSecond = MedianSlope() ?? BeatsPerSecond;
    }

    /// The first of the newest samples that all sit within the tolerance, once at least three of them span long enough.
    private int? StillSince(double tolerance, double quantum)
    {
        if (_samples.Count == 0) return null;
        var newest = _samples[^1];
        var first = _samples.Count - 1;
        while (first > 0 && Math.Abs(_samples[first - 1].Beats - newest.Beats) <= tolerance) first--;
        var speed = Math.Max(Math.Abs(BeatsPerSecond ?? 0), 0.5);
        var needed = Math.Min(Math.Max(quantum / speed, StillSpanMinimum), StillSpanMaximum);
        if (_samples.Count - first < 3 || !(newest.Time - _samples[first].Time >= needed)) return null;
        return first;
    }

    private double? MedianSlope()
    {
        if (_samples.Count < 2) return null;
        _slopes.Clear();
        CollectSlopes(MinimumPairSpan);
        if (_slopes.Count == 0 && BeatsPerSecond is null) CollectSlopes(0);
        if (_slopes.Count == 0) return null;
        _slopes.Sort();
        var middle = _slopes.Count / 2;
        return _slopes.Count % 2 == 1 ? _slopes[middle] : (_slopes[middle - 1] + _slopes[middle]) / 2;
    }

    private void CollectSlopes(double minimumSpan)
    {
        for (var i = 0; i < _samples.Count; i++)
        {
            for (var j = i + 1; j < _samples.Count; j++)
            {
                var span = _samples[j].Time - _samples[i].Time;
                if (span > 0 && span >= minimumSpan) _slopes.Add((_samples[j].Beats - _samples[i].Beats) / span);
            }
        }
    }
}
