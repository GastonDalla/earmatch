namespace Earmatch.Core;

public enum OverlayCoverKind
{
    /// The jog's BPM, under the BPM limit (black).
    JogBPM,

    /// The pitch percentage under the jog, in blind mode (black).
    Pitch,

    /// The BPM in the deck's header, in blind mode (rekordbox's header grey).
    HeaderBPM,

    /// A waveform in Hide Both (black).
    Waveform,
}

/// One cover over rekordbox, in the snapshot's physical screen pixels. Deck is 0 for the stacked waveform.
public readonly record struct OverlayCover(OverlayCoverKind Kind, int Deck, ScreenRect Frame);

/// Where Earmatch covers rekordbox from its UI Automation layout alone (OverlayController.swift's BPM masks, blind
/// covers and WaveformOverlayPlan's Hide Both panes). Without the captured pixels the jog BPM is covered fully under
/// every BPM limit, as the macOS mask does before it has measured the digits.
public static class OverlayCovers
{
    /// How far a jog BPM cover reaches past the text, in layout units: the macOS mask's cover without a measurement.
    public const double JogBPMMargin = 1;

    /// How far the blind mode covers reach past their text, in layout units (CoverView.margin).
    public const double BlindMargin = 1;

    /// The overlays show while Earmatch is on, rekordbox is in front in a supported layout, and Right Alt is not held.
    public static bool Shows(OverlaySettings settings, OverlayActivation activation, bool isPeeking) =>
        settings.IsEnabled && activation.IsActive && !isPeeking;

    /// The covers of the layout, waveforms first and then deck by deck; `scale` is the snapshot's pixels per layout unit.
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
