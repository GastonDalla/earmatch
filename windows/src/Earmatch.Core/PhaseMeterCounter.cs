namespace Earmatch.Core;

/// The counter under a deck's phase meter (OverlayController.counter on macOS).
public static class PhaseMeterCounter
{
    /// Bar position: the bar and beat rekordbox prints, from the deck's elapsed time and BPM, corrected by the drawn phase.
    /// Cue countdown: the beats to the next cue the last frame measured (none without a frame or a cue ahead), moved on
    /// at the deck's tempo from when that frame was taken to the time drawn.
    public static BarPosition? Position(CounterStyle style, DeckLayout deck, double? phase, double? cueBeatsAhead, double measuredAt,
        double beatsPerSecond, double time)
    {
        if (style == CounterStyle.BarPosition)
            return deck.ElapsedSeconds is { } elapsed && deck.BPM is { } bpm ? BarCounter.Position(elapsed, bpm, phase) : null;
        return cueBeatsAhead is { } measured ? CueCountdown.Counter(CueCountdown.BeatsAhead(measured, measuredAt, beatsPerSecond, time)) : null;
    }
}
