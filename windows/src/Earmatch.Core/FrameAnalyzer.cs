namespace Earmatch.Core;

public sealed record BPMCovers(ScreenRect OneDecimal, ScreenRect NoDecimals)
{
    public ScreenRect? Cover(BPMLimit limit) => limit switch
    {
        BPMLimit.OneDecimal => OneDecimal,
        BPMLimit.NoDecimals => NoDecimals,
        _ => null,
    };
}

public sealed record DeckFrameReading(
    BeatGridReading? Grid,
    PhaseEstimate Phase,
    double? BeatsPerSecond,
    bool IsMaster,
    BPMCovers? BPMCovers,
    double? CueBeatsAhead);

public readonly record struct FrameAnalysisOptions(bool MeasuresBPMGlyphs, bool ReadsCues);

public sealed class FrameAnalyzer
{
    private readonly object _gate = new();
    private readonly Dictionary<int, PhaseTracker> _trackers = [];
    private readonly Dictionary<int, (ScreenRect Strip, double Position)> _playheads = [];
    private double _trackerScale = 1;

    public IReadOnlyDictionary<int, DeckFrameReading> Analyze(PixelImage image, IReadOnlyList<DeckLayout> decks, FrameAnalysisOptions options,
        bool isMapped, double time)
    {
        lock (_gate)
        {
            if (image.ScreenUnitsPerPoint != _trackerScale)
            {
                _trackers.Clear();
                _trackerScale = image.ScreenUnitsPerPoint;
            }
            var readings = new Dictionary<int, DeckFrameReading>();
            foreach (var deck in decks) readings[deck.Number] = Read(deck, image, options, isMapped, time);
            return readings;
        }
    }

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
        if (!_trackers.TryGetValue(deck.Number, out var tracker)) _trackers[deck.Number] = tracker = new PhaseTracker(image.ScreenUnitsPerPoint);
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

    private double Playhead(int deck, WaveformStrip strip, PixelImage image)
    {
        if (PlayheadDetector.Locate(strip, image) is { } found)
        {
            _playheads[deck] = (strip.Frame, found);
            return found;
        }
        if (_playheads.TryGetValue(deck, out var known) && known.Strip == strip.Frame) return known.Position;
        return strip.Center + image.PointsInScreenUnits(0.5);
    }
}
