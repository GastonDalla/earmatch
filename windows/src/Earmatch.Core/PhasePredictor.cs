namespace Earmatch.Core;

/// A deck's phase as a frame left it: when, where in the bar, at what tempo, and whether it was measured (Live) or
/// held by the tracker.
public readonly record struct PhaseAnchor(double Time, double? PhaseInBar, double? BeatsPerSecond, bool IsMeasured);

/// Draws a deck's phase at each screen refresh, between and slightly past the captured frames: it extrapolates the last
/// measured phase at the measured tempo and eases the drawn phase towards it, so the meter moves smoothly, never steps
/// back while playing and jumps only on a seek.
public sealed class PhasePredictor
{
    public const double MaxExtrapolation = PhaseTracker.HoldDuration + 0.05;
    internal const double FollowTime = 0.025;
    internal const double SettleTime = 0.008;
    internal const double JumpTolerance = PhaseTracker.PhaseTolerance;
    internal const double MaxRefreshGap = 0.1;

    private readonly record struct Measured(double Time, double Phase, double? BeatsPerSecond);

    private Measured? _measured;
    private (double Time, double Phase)? _drawn;

    public double BeatsPerSecond => _measured?.BeatsPerSecond ?? 0;

    public void Update(PhaseAnchor anchor)
    {
        if (anchor.PhaseInBar is not { } phase)
        {
            Reset();
            return;
        }
        if (anchor.IsMeasured || _measured is null) _measured = new Measured(anchor.Time, phase, anchor.BeatsPerSecond);
        else _measured = _measured.Value with { BeatsPerSecond = anchor.BeatsPerSecond };
    }

    public void Reset()
    {
        _measured = null;
        _drawn = null;
    }

    /// Where the deck is at the time, from the last measurement; none once that is too old.
    public double? PredictedPhase(double time)
    {
        if (_measured is not { } measured || time - measured.Time > MaxExtrapolation) return null;
        return Numbers.PositiveModulo(measured.Phase + (measured.BeatsPerSecond ?? 0) * Math.Max(0, time - measured.Time), BeatGridDetector.BeatsPerBar);
    }

    /// The phase to draw at the time, eased from the previous drawn phase towards the prediction.
    public double? Phase(double time)
    {
        if (PredictedPhase(time) is not { } target)
        {
            _drawn = null;
            return null;
        }
        var rate = _measured?.BeatsPerSecond ?? 0;
        if (_drawn is not { } last || !(time > last.Time) || time - last.Time > MaxRefreshGap)
        {
            _drawn = (time, target);
            return target;
        }
        var elapsed = time - last.Time;
        var coasting = last.Phase + rate * elapsed;
        var error = PhaseTracker.CircularBeats(target - coasting);
        double step;
        if (Math.Abs(error) > JumpTolerance)
        {
            step = PhaseTracker.CircularBeats(target - last.Phase);
        }
        else
        {
            var gain = 1 - Math.Exp(-elapsed / (rate == 0 ? SettleTime : FollowTime));
            step = rate * elapsed + gain * error;
            if (rate > 0) step = Math.Max(step, 0);
            else if (rate < 0) step = Math.Min(step, 0);
        }
        var phase = Numbers.PositiveModulo(last.Phase + step, BeatGridDetector.BeatsPerBar);
        _drawn = (time, phase);
        return phase;
    }
}
