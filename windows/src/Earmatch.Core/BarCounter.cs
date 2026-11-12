namespace Earmatch.Core;

/// The bar and beat rekordbox prints near the playhead, from the elapsed time and BPM, corrected by the beat grid the
/// pixels show: the bar changes when the drawn downbeat passes, even if the clock is a little early or late.
public static class BarCounter
{
    public static BarPosition Position(double elapsedSeconds, double bpm, double? phaseInBar)
    {
        var beats = elapsedSeconds * bpm / 60;
        const double beatsPerBar = BeatGridDetector.BeatsPerBar;
        if (phaseInBar is not { } phase)
        {
            var bar = Math.Floor(beats / beatsPerBar);
            var beat = Math.Floor(Numbers.PositiveModulo(beats, beatsPerBar));
            return new BarPosition((int)bar + 1, (int)beat + 1);
        }
        var barNumber = Numbers.Round((beats - phase) / beatsPerBar);
        return new BarPosition((int)barNumber + 1, Math.Min((int)phase, (int)beatsPerBar - 1) + 1);
    }
}
