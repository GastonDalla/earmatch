namespace Earmatch.Core;

public static class PhaseMeterCounter
{
    public static BarPosition? Position(CounterStyle style, DeckLayout deck, double? phase, double? cueBeatsAhead, double measuredAt,
        double beatsPerSecond, double time)
    {
        if (style == CounterStyle.BarPosition)
            return deck.ElapsedSeconds is { } elapsed && deck.BPM is { } bpm ? BarCounter.Position(elapsed, bpm, phase) : null;
        if (time - measuredAt > PhasePredictor.MaxExtrapolation) return null;
        return cueBeatsAhead is { } measured ? CueCountdown.Counter(CueCountdown.BeatsAhead(measured, measuredAt, beatsPerSecond, time)) : null;
    }
}
