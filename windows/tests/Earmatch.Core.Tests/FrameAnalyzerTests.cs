using Earmatch.Core;
using static Earmatch.Core.Tests.Fixture;

namespace Earmatch.Core.Tests;

/// The per-frame reading of every deck (the pure part of FrameAnalyzer.swift), on the macOS 2-deck frames in points.
public class FrameAnalyzerTests
{
    static readonly ScreenPoint CropOrigin = new(0, 136);
    static readonly FrameAnalysisOptions Everything = new(MeasuresBPMGlyphs: true, ReadsCues: true);

    static DeckLayout Deck(int number, ScreenRect strip, ScreenRect master, bool loaded = true) =>
        new(number, loaded ? $"Track {number}" : null, loaded ? 128 : null, null, Rect(558 + 332 * (number - 1), 427, 54, 23), null,
            master, new WaveformStrip(strip, StripOrientation.Horizontal), null);

    static readonly DeckLayout[] Decks =
    [
        Deck(1, Rect(0, 136, 1492, 69), Rect(612, 311, 70, 13)),
        Deck(2, Rect(0, 205, 1492, 69), Rect(1414, 311, 70, 13)),
    ];

    static DeckLayout InPhysicalPixels(DeckLayout deck) => deck with
    {
        JogBPMFrame = deck.JogBPMFrame is { } jog ? Physical.Rect(jog) : null,
        MasterButtonFrame = Physical.Rect(deck.MasterButtonFrame),
        Strip = deck.Strip is { } strip ? Physical.Strip(strip) : null,
    };

    [Fact]
    public void ReadsEachDecksGridPhaseAndSyncMaster()
    {
        var image = PixelBuffer.Png("waveform-playing-2.png").Image(CropOrigin);

        var readings = new FrameAnalyzer().Analyze(image, Decks, Everything, isMapped: true, time: 1);

        Assert.Equal([1, 2], readings.Keys.Order());
        foreach (var deck in Decks)
        {
            var reading = readings[deck.Number];
            var playhead = PlayheadDetector.Locate(deck.Strip!, image);
            Assert.Equal(746.5, playhead);
            var grid = BeatGridDetector.Read(deck.Strip!, playhead!.Value, image);
            Assert.NotNull(grid);
            Assert.Equal(grid, reading.Grid);
            Assert.Equal(new PhaseEstimate(grid.PhaseInBar, grid.BeatSpacing, PhaseTrackerStatus.Live), reading.Phase);
            Assert.Null(reading.BeatsPerSecond);
            Assert.Equal(deck.JogBPMFrame, reading.BPMCovers?.OneDecimal);
            Assert.Equal(deck.JogBPMFrame, reading.BPMCovers?.NoDecimals);
            Assert.Null(reading.CueBeatsAhead);
        }
        Assert.True(readings[1].IsMaster);
        Assert.False(readings[2].IsMaster);
    }

    [Fact]
    public void WithoutAPlayheadLineTheStripsCentreIsThePlayheadAndCuesAheadAreCounted()
    {
        var image = PixelBuffer.Png("waveform-playing-0.png").Image(CropOrigin);
        var deck = Deck(2, Rect(0, 205, 600, 69), Rect(1414, 311, 70, 13));

        var reading = new FrameAnalyzer().Analyze(image, [deck], Everything, isMapped: true, time: 0)[2];

        Assert.NotNull(reading.Grid);
        Assert.Equal(BeatGridDetector.Read(deck.Strip!, 300.5, image), reading.Grid);
        Assert.Equal(CueCountdown.BeatsToNextCue(deck.Strip!.Frame, 300.5, reading.Grid.BeatSpacing, image), reading.CueBeatsAhead);
        Assert.Equal("01.3", CueCountdown.Counter(reading.CueBeatsAhead)?.Label);
    }

    [Fact]
    public void OptionsLeaveOutTheCoversAndTheCues()
    {
        var image = PixelBuffer.Png("waveform-playing-0.png").Image(CropOrigin);
        var deck = Deck(2, Rect(0, 205, 600, 69), Rect(1414, 311, 70, 13));

        var reading = new FrameAnalyzer().Analyze(image, [deck], new FrameAnalysisOptions(MeasuresBPMGlyphs: false, ReadsCues: false), true, 0)[2];

        Assert.NotNull(reading.Grid);
        Assert.Null(reading.BPMCovers);
        Assert.Null(reading.CueBeatsAhead);
    }

    [Fact]
    public void AnEmptyDeckIsNotRead()
    {
        var image = PixelBuffer.Png("waveform-playing-2.png").Image(CropOrigin);
        var empty = Deck(1, Rect(0, 136, 1492, 69), Rect(612, 311, 70, 13), loaded: false);

        var reading = new FrameAnalyzer().Analyze(image, [empty], Everything, isMapped: true, time: 0)[1];

        Assert.Null(reading.Grid);
        Assert.Equal(new PhaseEstimate(null, null, PhaseTrackerStatus.Lost), reading.Phase);
        Assert.Null(reading.CueBeatsAhead);
    }

    [Fact]
    public void AnUnmappedFrameReadsNothingAndCoversTheWholeJogDisplays()
    {
        var image = PixelBuffer.Png("waveform-playing-2.png").Image(CropOrigin);

        var readings = new FrameAnalyzer().Analyze(image, Decks, Everything, isMapped: false, time: 0);

        foreach (var deck in Decks)
        {
            var reading = readings[deck.Number];
            Assert.Null(reading.Grid);
            Assert.Equal(PhaseTrackerStatus.Lost, reading.Phase.Status);
            Assert.False(reading.IsMaster);
            Assert.Equal(new BPMCovers(deck.JogBPMFrame!.Value, deck.JogBPMFrame!.Value), reading.BPMCovers);
        }
    }

    [Fact]
    public void EachDeckKeepsItsTrackerAcrossFramesUntilReset()
    {
        var image = PixelBuffer.Png("waveform-playing-2.png").Image(CropOrigin);
        var analyzer = new FrameAnalyzer();

        var live = analyzer.Analyze(image, Decks, Everything, isMapped: true, time: 0)[1];
        var held = analyzer.Analyze(image, Decks, Everything, isMapped: false, time: 0.1)[1];
        var lost = analyzer.Analyze(image, Decks, Everything, isMapped: false, time: 0.5)[1];
        analyzer.Analyze(image, Decks, Everything, isMapped: true, time: 0.6);
        analyzer.Reset();
        var afterReset = analyzer.Analyze(image, Decks, Everything, isMapped: false, time: 0.61)[1];

        Assert.Equal(PhaseTrackerStatus.Live, live.Phase.Status);
        Assert.Equal(new PhaseEstimate(live.Phase.PhaseInBar, live.Phase.BeatSpacing, PhaseTrackerStatus.Held), held.Phase);
        Assert.Equal(PhaseTrackerStatus.Lost, lost.Phase.Status);
        Assert.Equal(PhaseTrackerStatus.Lost, afterReset.Phase.Status);
    }

    /// The 4-deck vertical column's playhead (235) is half a point past the centre fallback (234.5): once found, it stays
    /// where it was while a frame does not show it.
    [Fact]
    public void AStripWhosePlayheadDisappearsKeepsTheLastOneFound()
    {
        var origin = new ScreenPoint(622, 136);
        var strip = new WaveformStrip(Rect(684, 154, 62, 160), StripOrientation.Vertical, Rect(684, 164, 62, 150));
        var deck = new DeckLayout(1, "techniuto", 135, null, null, null, Rect(541, 168, 70, 13), strip, null);
        var shown = PixelBuffer.Png("waveform-4deck-vertical.png");
        var hidden = PixelBuffer.Png("waveform-4deck-vertical.png");
        hidden.Paint(0, 190, hidden.Width, 12, (0, 0, 0));
        var analyzer = new FrameAnalyzer();

        analyzer.Analyze(shown.Image(origin), [deck], Everything, isMapped: true, time: 0);
        var reading = analyzer.Analyze(hidden.Image(origin), [deck], Everything, isMapped: true, time: 0.01)[1];

        Assert.Equal(235, PlayheadDetector.Locate(strip, shown.Image(origin)));
        Assert.Null(PlayheadDetector.Locate(strip, hidden.Image(origin)));
        var kept = BeatGridDetector.Read(strip, 235, hidden.Image(origin));
        Assert.NotNull(kept);
        Assert.Equal(kept, reading.Grid);
        Assert.NotEqual(BeatGridDetector.Read(strip, 234.5, hidden.Image(origin)), reading.Grid);
        Assert.Equal(BeatGridDetector.Read(strip, 234.5, hidden.Image(origin)),
            new FrameAnalyzer().Analyze(hidden.Image(origin), [deck], Everything, isMapped: true, time: 0)[1].Grid);
    }

    /// rekordbox for Windows at 200%: the same frame with the decks in physical pixels reads the doubled grid, the same
    /// phase and sync master.
    [Fact]
    public void PhysicalPixelCoordinatesReadTheSameDecks()
    {
        var buffer = PixelBuffer.Png("waveform-playing-0.png");
        var points = new FrameAnalyzer().Analyze(buffer.Image(CropOrigin), Decks, Everything, true, 0);
        var physical = new FrameAnalyzer().Analyze(buffer.WindowsImage(CropOrigin), Decks.Select(InPhysicalPixels).ToList(), Everything, true, 0);

        foreach (var deck in Decks)
        {
            var (inPoints, inPixels) = (points[deck.Number], physical[deck.Number]);
            Assert.Equal(inPoints.Grid!.BeatXs.Select(x => x * 2), inPixels.Grid!.BeatXs);
            Assert.Equal(inPoints.Phase.PhaseInBar!.Value, inPixels.Phase.PhaseInBar!.Value, 9);
            Assert.Equal(inPoints.IsMaster, inPixels.IsMaster);
            Assert.Equal(Physical.Rect(inPoints.BPMCovers!.OneDecimal), inPixels.BPMCovers!.OneDecimal);
        }
    }

    [Fact]
    public void TheCoverForTheLimitIsTheMeasuredOne()
    {
        var covers = new BPMCovers(Rect(1, 2, 3, 4), Rect(5, 6, 7, 8));

        Assert.Equal(Rect(1, 2, 3, 4), covers.Cover(BPMLimit.OneDecimal));
        Assert.Equal(Rect(5, 6, 7, 8), covers.Cover(BPMLimit.NoDecimals));
        Assert.Null(covers.Cover(BPMLimit.CoverFully));
    }
}

/// The counter under each meter (OverlayController.counter on macOS).
public class PhaseMeterCounterTests
{
    static readonly DeckLayout Deck = new(1, "Track", 124, null, null, null, Rect(0, 0, 70, 13), null, null, elapsedSeconds: 10.8);

    [Fact]
    public void TheBarPositionFollowsTheClockCorrectedByTheDrawnPhase()
    {
        Assert.Equal(new BarPosition(6, 3), PhaseMeterCounter.Position(CounterStyle.BarPosition, Deck, phase: 2.3, null, 0, 0, 0));
        Assert.Equal(new BarPosition(6, 3), PhaseMeterCounter.Position(CounterStyle.BarPosition, Deck, phase: null, null, 0, 0, 0));
        Assert.Null(PhaseMeterCounter.Position(CounterStyle.BarPosition, Deck with { ElapsedSeconds = null }, 2.3, null, 0, 0, 0));
        Assert.Null(PhaseMeterCounter.Position(CounterStyle.BarPosition, Deck with { BPM = null }, 2.3, null, 0, 0, 0));
    }

    [Fact]
    public void TheCueCountdownMovesOnFromTheLastMeasurementAtTheDecksTempo()
    {
        Assert.Equal("01.3", PhaseMeterCounter.Position(CounterStyle.CueCountdown, Deck, 2.3, cueBeatsAhead: 10, measuredAt: 2, beatsPerSecond: 2, time: 3.5)?.Label);
        Assert.Null(PhaseMeterCounter.Position(CounterStyle.CueCountdown, Deck, 2.3, cueBeatsAhead: null, measuredAt: 2, beatsPerSecond: 2, time: 3.5));
        Assert.Null(PhaseMeterCounter.Position(CounterStyle.CueCountdown, Deck, 2.3, cueBeatsAhead: 1, measuredAt: 2, beatsPerSecond: 2, time: 3.5));
    }
}
