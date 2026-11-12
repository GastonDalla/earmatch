using Earmatch.Core;
using static Earmatch.Core.Tests.Fixture;

namespace Earmatch.Core.Tests;

public class CueCountdownTests
{
    static readonly ScreenPoint CropOrigin = new(0, 136);
    static readonly ScreenRect Deck1Strip = Rect(0, 136, 1492, 69);
    static readonly ScreenRect Deck2Strip = Rect(0, 205, 1492, 69);
    const double PlayheadX = 746;

    static ValueList<CueMarker> Markers(string name, ScreenRect strip) =>
        CueCountdown.Markers(strip, strip.MinX, PixelBuffer.Png(name).Image(CropOrigin));

    [Fact]
    public void FindsTheHotCueFlagAndTheCuePointTriangleInRealFrames()
    {
        var deck1 = Markers("waveform-playing-0.png", Deck1Strip);
        var deck2 = Markers("waveform-playing-0.png", Deck2Strip);

        Assert.Equal(2, deck1.Count);
        Assert.Equal(new HashSet<CueMarkerKind> { CueMarkerKind.HotCue, CueMarkerKind.CuePoint }, deck1.Select(marker => marker.Kind).ToHashSet());
        Assert.All(deck1, marker => Assert.True(Math.Abs(marker.X - 491.5) <= 1, $"{marker.X}"));
        Assert.Equal(2, deck2.Count);
        Assert.Contains(deck2, marker => marker.Kind == CueMarkerKind.CuePoint && Math.Abs(marker.X - 491.5) <= 1);
        Assert.Contains(deck2, marker => marker.Kind == CueMarkerKind.HotCue && Math.Abs(marker.X - 610.5) <= 1);
    }

    [Fact]
    public void DownbeatTrianglesBeatTicksAndTheWaveformAreNotCues()
    {
        foreach (var name in new[] { "waveform-playing-0.png", "waveform-playing-2.png", "waveform-playing-4.png" })
        {
            var image = PixelBuffer.Png(name).Image(CropOrigin);
            foreach (var strip in new[] { Deck1Strip, Deck2Strip })
            {
                Assert.True(CueCountdown.Markers(strip, PlayheadX, image).Count == 0, name);
            }
        }
    }

    [Fact]
    public void WithNoCueAheadTheCountdownReadsDashes()
    {
        var image = PixelBuffer.Png("waveform-playing-0.png").Image(CropOrigin);
        var grid = BeatGridDetector.Read(Deck1Strip, PlayheadX, image);

        Assert.NotNull(grid);
        var beats = CueCountdown.BeatsToNextCue(Deck1Strip, PlayheadX, grid.BeatSpacing, image);

        Assert.Null(beats);
        Assert.Null(CueCountdown.Counter(beats));
    }

    [Fact]
    public void CountsDownToTheNearestCueAheadInBarsAndBeats()
    {
        var image = PixelBuffer.Png("waveform-playing-0.png").Image(CropOrigin);
        var spacing1 = BeatGridDetector.Read(Deck1Strip, PlayheadX, image)!.BeatSpacing;
        var spacing2 = BeatGridDetector.Read(Deck2Strip, PlayheadX, image)!.BeatSpacing;
        string? Countdown(ScreenRect strip, double x, double spacing) =>
            CueCountdown.Counter(CueCountdown.BeatsToNextCue(strip, x, spacing, image))?.Label;

        Assert.Equal("01.3", Countdown(Deck2Strip, 300, spacing2));
        Assert.Equal("00.3", Countdown(Deck2Strip, 540, spacing2));
        Assert.Equal("00.1", Countdown(Deck1Strip, 470, spacing1));
    }

    [Fact]
    public void CueMarkersScrollWithTheBeatGrid()
    {
        static (double HotCue, double Phase, double Spacing) Reading(string name, ScreenRect strip)
        {
            var image = PixelBuffer.Png(name).Image(CropOrigin);
            var grid = BeatGridDetector.Read(strip, PlayheadX, image)!;
            var hotCue = CueCountdown.Markers(strip, strip.MinX, image).First(marker => marker.Kind == CueMarkerKind.HotCue);
            return (hotCue.X, grid.PhaseInBar!.Value, grid.BeatSpacing);
        }
        foreach (var strip in new[] { Deck1Strip, Deck2Strip })
        {
            var start = Reading("waveform-playing-0.png", strip);
            var end = Reading("waveform-playing-4.png", strip);
            var gridAdvance = (end.Phase - start.Phase + 4) % 4;
            var cueAdvance = (start.HotCue - end.HotCue) / start.Spacing;

            Assert.True(Math.Abs(cueAdvance - gridAdvance) < 0.1, $"{cueAdvance} vs {gridAdvance}");
        }
    }

    [Fact]
    public void AnOrangeWaveformBodyIsNotACuePoint()
    {
        var buffer = new PixelBuffer(1200, 140);
        buffer.Paint(400, 10, 40, 128, (240, 150, 40));

        Assert.Empty(CueCountdown.Markers(Rect(0, 0, 600, 70), 0, buffer.Image()));
    }

    static PixelBuffer DrawnCues()
    {
        var buffer = new PixelBuffer(1200, 140);
        for (var row = 0; row < 20; row++)
        {
            var half = 1 + row * 14 / 19;
            buffer.Paint(600 - half, 116 + row, 2 * half, 1, (247, 160, 48));
        }
        buffer.Paint(789, 0, 22, 22, (255, 55, 111));
        return buffer;
    }

    [Fact]
    public void ADrawnCuePointTriangleAndHotCueFlagAreFoundInSRGB()
    {
        var found = CueCountdown.Markers(Rect(0, 0, 600, 70), 0, DrawnCues().Image());

        Assert.Equal([CueMarkerKind.CuePoint, CueMarkerKind.HotCue], found.Select(marker => marker.Kind));
        Assert.True(Math.Abs(found[0].X - 300) <= 0.5);
        Assert.True(Math.Abs(found[1].X - 400) <= 0.5);
    }

    [Fact]
    public void DistancesBecomeBarsAndBeatsLikeThePlayers()
    {
        Assert.Equal("00.1", CueCountdown.Counter(0.3)?.Label);
        Assert.Equal("00.4", CueCountdown.Counter(4)?.Label);
        Assert.Equal("01.1", CueCountdown.Counter(4.2)?.Label);
        Assert.Equal("06.2", CueCountdown.Counter(26)?.Label);
        Assert.Null(CueCountdown.Counter(0));
        Assert.Null(CueCountdown.Counter(-1));
    }

    [Fact]
    public void TheCountdownMovesOnAtTheDecksTempoBetweenFrames()
    {
        Assert.Equal(7, CueCountdown.BeatsAhead(10, measuredAt: 2, beatsPerSecond: 2, time: 3.5));
        Assert.Equal(10, CueCountdown.BeatsAhead(10, measuredAt: 2, beatsPerSecond: 0, time: 9));
    }

    /// rekordbox for Windows at 200%: the same frames in physical pixels find the cues at the doubled positions and count
    /// the same beats.
    [Fact]
    public void PhysicalPixelCoordinatesFindTheSameCues()
    {
        var buffer = PixelBuffer.Png("waveform-playing-0.png");
        foreach (var strip in new[] { Deck1Strip, Deck2Strip })
        {
            var points = CueCountdown.Markers(strip, strip.MinX, buffer.Image(CropOrigin));
            var physical = CueCountdown.Markers(Physical.Rect(strip), strip.MinX * 2, buffer.WindowsImage(CropOrigin));
            Assert.Equal(points.Select(marker => marker with { X = marker.X * 2 }), physical);
        }
        var spacing = BeatGridDetector.Read(Deck2Strip, PlayheadX, buffer.Image(CropOrigin))!.BeatSpacing;
        Assert.Equal(CueCountdown.BeatsToNextCue(Deck2Strip, 300, spacing, buffer.Image(CropOrigin)),
            CueCountdown.BeatsToNextCue(Physical.Rect(Deck2Strip), 600, spacing * 2, buffer.WindowsImage(CropOrigin)));
        var drawn = DrawnCues();
        Assert.Equal([CueMarkerKind.CuePoint, CueMarkerKind.HotCue],
            CueCountdown.Markers(Rect(0, 0, 1200, 140), 0, drawn.WindowsImage()).Select(marker => marker.Kind));
    }
}
