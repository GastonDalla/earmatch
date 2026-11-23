namespace Earmatch.Core;

public static class WaveformScroll
{
    public const double MaxShiftBeats = PhaseTracker.PhaseTolerance;

    public static double Shift(double? framePhase, double? drawnPhase, double? beatSpacing, double pixelsPerUnit)
    {
        if (framePhase is not { } frame || drawnPhase is not { } drawn || beatSpacing is not { } spacing || !(spacing > 0)) return 0;
        var beats = PhaseTracker.CircularBeats(drawn - frame);
        return Math.Abs(beats) <= MaxShiftBeats ? Numbers.Round(beats * spacing * pixelsPerUnit) : 0;
    }
}
