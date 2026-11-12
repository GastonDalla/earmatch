namespace Earmatch.Core;

public enum OverlayCoverKind
{
    JogBPM,

    Pitch,

    HeaderBPM,

    Waveform,
}

public readonly record struct OverlayCover(OverlayCoverKind Kind, int Deck, ScreenRect Frame);

public static class OverlayCovers
{
    public const double JogBPMMargin = 1;

    public const double BlindMargin = 1;

    public static bool Shows(OverlaySettings settings, OverlayActivation activation, bool isPeeking) =>
        settings.IsEnabled && activation.IsActive && !isPeeking;

    public static IReadOnlyList<OverlayCover> Plan(RekordboxLayout layout, OverlaySettings settings, double scale)
    {
        var covers = new List<OverlayCover>();
        if (settings.WaveformMode == WaveformMode.HideBoth && layout.Arrangement is { } arrangement && layout.StackedWaveform is { } stacked)
        {
            if (arrangement.Orientation == StripOrientation.Horizontal)
            {
                covers.Add(new OverlayCover(OverlayCoverKind.Waveform, 0, stacked));
            }
            else
            {
                foreach (var deck in layout.Decks)
                {
                    if (deck.WaveformStrip is { } strip) covers.Add(new OverlayCover(OverlayCoverKind.Waveform, deck.Number, strip));
                }
            }
        }
        foreach (var deck in layout.Decks)
        {
            if (settings.IsBPMLimitEnabled && deck.JogBPMFrame is { } jog)
            {
                covers.Add(new OverlayCover(OverlayCoverKind.JogBPM, deck.Number, Outset(jog, JogBPMMargin * scale)));
            }
            if (!settings.IsBlindModeEnabled) continue;
            if (deck.PitchFrame is { } pitch) covers.Add(new OverlayCover(OverlayCoverKind.Pitch, deck.Number, Outset(pitch, BlindMargin * scale)));
            if (deck.HeaderBPMFrame is { } header)
            {
                covers.Add(new OverlayCover(OverlayCoverKind.HeaderBPM, deck.Number, Outset(header, BlindMargin * scale)));
            }
        }
        return covers;
    }

    private static ScreenRect Outset(ScreenRect rect, double by) => new(rect.X - by, rect.Y - by, rect.Width + 2 * by, rect.Height + 2 * by);
}
