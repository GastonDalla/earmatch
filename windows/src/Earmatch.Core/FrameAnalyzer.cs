namespace Earmatch.Core;

/// Where the limit BPM cover goes on a jog display for the two limits that keep digits.
public sealed record BPMCovers(ScreenRect OneDecimal, ScreenRect NoDecimals)
{
    /// The measured cover for the limit; none for Cover fully, which covers the whole display.
    public ScreenRect? Cover(BPMLimit limit) => limit switch
    {
        BPMLimit.OneDecimal => OneDecimal,
        BPMLimit.NoDecimals => NoDecimals,
        _ => null,
    };
}

/// One deck as one frame shows it: the beat grid, the tracked phase and tempo, whether its MASTER button is lit, the
/// jog BPM covers and how many beats ahead the next cue is.
public sealed record DeckFrameReading(
    BeatGridReading? Grid,
    PhaseEstimate Phase,
    double? BeatsPerSecond,
    bool IsMaster,
    BPMCovers? BPMCovers,
    double? CueBeatsAhead);

/// What a frame is read for besides the grid: the jog BPM digits (while Limit BPM display is on) and the cues (for the
/// cue countdown counter).
public readonly record struct FrameAnalysisOptions(bool MeasuresBPMGlyphs, bool ReadsCues);

/// Reads every deck from one captured frame of the rekordbox window (the platform-independent part of the macOS
/// FrameAnalyzer): finds each strip's playhead, reads its beat grid, tracks the phase and tempo per deck across frames,
/// and measures the sync master, the BPM digit covers and the next cue. The frame's PixelImage and the decks' frames
/// share screen coordinates. Safe to call from the capture thread while another thread resets it.
public sealed class FrameAnalyzer
{
    private readonly object _gate = new();
    private readonly Dictionary<int, PhaseTracker> _trackers = [];
    private readonly Dictionary<int, (ScreenRect Strip, double Position)> _playheads = [];
    private double _trackerScale = 1;

    /// The readings of the decks, by deck number. An unmapped frame (one that does not show the window as the layout
    /// describes it) only advances the trackers and covers the jog displays whole.
    public IReadOnlyDictionary<int, DeckFrameReading> Analyze(PixelImage image, IReadOnlyList<DeckLayout> decks, FrameAnalysisOptions options,
        bool isMapped, double time)
    {
        lock (_gate)
        {
            if (image.LayoutScale != _trackerScale)
            {
                _trackers.Clear();
                _trackerScale = image.LayoutScale;
            }
            var readings = new Dictionary<int, DeckFrameReading>();
            foreach (var deck in decks) readings[deck.Number] = Read(deck, image, options, isMapped, time);
            return readings;
        }
    }

    /// Forgets the trackers and playheads, for a new capture or layout.
    public void Reset()
    {
        lock (_gate)
        {
            _trackers.Clear();
            _playheads.Clear();
        }
    }

    private DeckFrameReading Read(DeckLayout deck, PixelImage image, FrameAnalysisOptions options, bool isMapped, double time)
    {
        var isLoaded = deck.BPM is not null || deck.Title is not null;
        double? playhead = isMapped && isLoaded && deck.Strip is { } shown ? Playhead(deck.Number, shown, image) : null;
        var grid = deck.Strip is { } strip && playhead is { } position ? BeatGridDetector.Read(strip, position, image) : null;
        if (!_trackers.TryGetValue(deck.Number, out var tracker)) _trackers[deck.Number] = tracker = new PhaseTracker(image.LayoutScale);
        var phase = tracker.Update(grid, time);

        BPMCovers? covers = null;
        if (options.MeasuresBPMGlyphs && deck.JogBPMFrame is { } jog)
        {
            covers = isMapped
                ? new BPMCovers(BPMGlyphMask.Cover(BPMLimit.OneDecimal, jog, image), BPMGlyphMask.Cover(BPMLimit.NoDecimals, jog, image))
                : new BPMCovers(jog, jog);
        }
        double? cues = null;
        if (options.ReadsCues && deck.Strip is { Orientation: StripOrientation.Horizontal } horizontal && playhead is { } x && phase.BeatSpacing is { } spacing)
            cues = CueCountdown.BeatsToNextCue(horizontal.Frame, x, spacing, image);

        return new DeckFrameReading(grid, phase, tracker.BeatsPerSecond, isMapped && SyncMasterDetector.IsMaster(deck.MasterButtonFrame, image),
            covers, cues);
    }

    /// The playhead line in the frame; while a frame does not show it, the last one found on the same strip, or half a
    /// point past the strip's centre, where rekordbox draws it.
    private double Playhead(int deck, WaveformStrip strip, PixelImage image)
    {
        if (PlayheadDetector.Locate(strip, image) is { } found)
        {
            _playheads[deck] = (strip.Frame, found);
            return found;
        }
        if (_playheads.TryGetValue(deck, out var known) && known.Strip == strip.Frame) return known.Position;
        return strip.Center + image.Points(0.5);
    }
}
