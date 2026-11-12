using Earmatch.Core;
using static Earmatch.Core.Tests.Fixture;

namespace Earmatch.Core.Tests;

public class LayoutPixelsTests
{
    sealed record Frame(string File, ScreenPoint Origin, IReadOnlyDictionary<int, WaveformStrip> Strips);

    static WaveformStrip Vertical(ScreenRect frame, double markerTop) =>
        new(frame, StripOrientation.Vertical, Rect(frame.X, markerTop, frame.Width, frame.MaxY - markerTop));

    static WaveformStrip Horizontal(ScreenRect frame) => new(frame, StripOrientation.Horizontal);

    static readonly Frame TwoVertical = new("waveform-2deck-vertical.png", new ScreenPoint(622, 136), new Dictionary<int, WaveformStrip>
    {
        [1] = Vertical(Rect(622, 136, 124, 140), 164),
        [2] = Vertical(Rect(746, 136, 124, 140), 164),
    });

    static readonly Frame FourVertical = new("waveform-4deck-vertical.png", new ScreenPoint(622, 136), new Dictionary<int, WaveformStrip>
    {
        [1] = Vertical(Rect(684, 154, 62, 160), 164),
        [2] = Vertical(Rect(746, 154, 62, 160), 164),
        [3] = Vertical(Rect(622, 154, 62, 160), 164),
        [4] = Vertical(Rect(808, 154, 62, 160), 164),
    });

    static readonly Frame FourHorizontal = new("waveform-4deck-horizontal.png", new ScreenPoint(0, 136), new Dictionary<int, WaveformStrip>
    {
        [1] = Horizontal(Rect(12, 136, 1468, 48)),
        [2] = Horizontal(Rect(12, 184, 1468, 48)),
        [3] = Horizontal(Rect(12, 233, 1468, 48)),
        [4] = Horizontal(Rect(12, 281, 1468, 48)),
    });

    static PixelImage Image(Frame frame) => PixelBuffer.Png(frame.File).Image(frame.Origin);

    [Fact]
    public void FindsThePlayheadLineRekordboxDrawsJustPastTheStripsCentre()
    {
        Frame[] frames = [TwoVertical, TwoVertical, FourVertical, FourVertical, FourHorizontal, FourHorizontal];
        int[] decks = [1, 2, 1, 2, 1, 2];
        double[] expectations = [206.5, 206.5, 235, 235, 746.5, 746.5];
        for (var index = 0; index < frames.Length; index++)
        {
            var frame = frames[index];
            var found = PlayheadDetector.Locate(frame.Strips[decks[index]], Image(frame));
            Assert.True(found == expectations[index], $"{frame.File} deck {decks[index]}: {found}");
        }
    }

    [Fact]
    public void TheStackedTwoDeckPlayheadIsFoundToo()
    {
        var strip = Horizontal(Rect(0, 136, 1492, 69));
        Assert.Equal(746.5, PlayheadDetector.Locate(strip, PixelBuffer.Png("waveform-playing-2.png").Image(new ScreenPoint(0, 136))));
    }

    [Fact]
    public void AnEmptyDeckHasNoPlayhead()
    {
        foreach (var (frame, deck) in new[] { (FourVertical, 3), (FourHorizontal, 4) })
        {
            Assert.Null(PlayheadDetector.Locate(frame.Strips[deck], Image(frame)));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ReadsTheTwoDeckVerticalColumnsGrid(int deck)
    {
        var reading = BeatGridDetector.Read(TwoVertical.Strips[deck], 206.5, Image(TwoVertical));

        Assert.NotNull(reading);
        Assert.True(Math.Abs(reading.BeatSpacing - 10.5) < 0.4, $"{reading.BeatSpacing}");
        Assert.All(reading.BeatXs, y => Assert.True(y >= 164 && y < 206, $"{y}"));
        Assert.True(reading.BeatXs.Count >= 3);
        Assert.Equal(3, (int)reading.PhaseInBar!.Value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ReadsTheFourDeckVerticalSixPointBeats(int deck)
    {
        var reading = BeatGridDetector.Read(FourVertical.Strips[deck], 235, Image(FourVertical));

        Assert.NotNull(reading);
        Assert.True(Math.Abs(reading.BeatSpacing - 6) < 0.2, $"{reading.BeatSpacing}");
        Assert.True(reading.BeatXs.Count >= 10);
        Assert.Equal(2, reading.DownbeatXs.Count);
        Assert.All(reading.DownbeatXs, downbeat => Assert.True(Math.Abs(downbeat - 188) < 0.6 || Math.Abs(downbeat - 212) < 0.6, $"{downbeat}"));
        var phase = reading.PhaseInBar!.Value;
        Assert.Equal(3, (int)phase);
        Assert.True(phase > 3.75, $"{phase}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ReadsTheFourDeckHorizontalStrips(int deck)
    {
        var reading = BeatGridDetector.Read(FourHorizontal.Strips[deck], 746.5, Image(FourHorizontal));

        Assert.NotNull(reading);
        Assert.All(reading.BeatXs, x => Assert.True(x > 12 && x < 746.5, $"{x}"));
        Assert.True(reading.DownbeatXs.Count >= 4);
        Assert.Equal(3, (int)reading.PhaseInBar!.Value);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void EmptyDecksHaveNoGrid(int deck)
    {
        foreach (var frame in new[] { FourVertical, FourHorizontal })
        {
            var strip = frame.Strips[deck];
            Assert.Null(BeatGridDetector.Read(strip, strip.Center, Image(frame)));
        }
    }

    [Fact]
    public void TheButtonBarOverTheColumnsTopIsNotReadAsMarkers()
    {
        var reading = BeatGridDetector.Read(FourVertical.Strips[1], 235, Image(FourVertical));

        Assert.NotNull(reading);
        Assert.All(reading.BeatXs, y => Assert.True(y >= 164, $"{y}"));
    }

    static PixelBuffer VerticalMarkers()
    {
        var buffer = new PixelBuffer(120, 600);
        for (var beat = 0; beat < 10; beat++)
        {
            var y = (15 + beat * 30) * 2;
            if (beat % 4 == 1)
            {
                buffer.Paint(2, y - 5, 6, 11, (255, 0, 0));
                buffer.Paint(112, y - 5, 6, 11, (255, 0, 0));
            }
            else
            {
                buffer.Paint(2, y - 2, 2, 6, (124, 124, 124));
                buffer.Paint(2, y, 7, 1, (124, 124, 124));
                buffer.Paint(114, y - 2, 2, 6, (124, 124, 124));
                buffer.Paint(111, y, 7, 1, (124, 124, 124));
            }
        }
        buffer.Paint(0, 300, 120, 2, (112, 112, 112));
        return buffer;
    }

    [Fact]
    public void ReadsSyntheticVerticalMarkersOnBothEdges()
    {
        var image = VerticalMarkers().Image();
        var strip = new WaveformStrip(Rect(0, 0, 60, 300), StripOrientation.Vertical);

        var reading = BeatGridDetector.Read(strip, 150.5, image);
        var playhead = PlayheadDetector.Locate(strip, image);

        Assert.Equal(150.5, playhead);
        Assert.NotNull(reading);
        Assert.True(Math.Abs(reading.BeatSpacing - 30) < 0.1);
        Assert.Equal(3, reading.DownbeatXs.Count);
        Assert.True(Math.Abs(reading.PhaseInBar!.Value - (150.5 - 45) / 30) < 0.03);
    }

    [Fact]
    public void AMarkOnOneEdgeOnlyIsNotABeat()
    {
        var buffer = new PixelBuffer(120, 600);
        for (var beat = 0; beat < 10; beat++)
        {
            var y = (15 + beat * 30) * 2;
            buffer.Paint(2, y - 2, 7, 6, (124, 124, 124));
            buffer.Paint(111, y - 2, 7, 6, (124, 124, 124));
        }
        buffer.Paint(2, 2 * 62, 7, 6, (124, 124, 124));
        var strip = new WaveformStrip(Rect(0, 0, 60, 300), StripOrientation.Vertical);

        var reading = BeatGridDetector.Read(strip, 150.5, buffer.Image());

        Assert.NotNull(reading);
        Assert.DoesNotContain(reading.BeatXs, y => Math.Abs(y - 63.5) < 2);
        Assert.True(Math.Abs(reading.BeatSpacing - 30) < 0.1);
    }

    [Theory]
    [InlineData("horizontal", 361, 559)]
    [InlineData("vertical", 168, 384)]
    public void SpotsTheSyncMasterAmongFourDecks(string layout, double topRow, double bottomRow)
    {
        ScreenRect[] buttons = [Rect(layout == "horizontal" ? 570 : 541, topRow, 70, 13), Rect(1414, topRow, 70, 13),
            Rect(layout == "horizontal" ? 570 : 541, bottomRow, 70, 13), Rect(1414, bottomRow, 70, 13)];
        var masters = new List<int>();
        for (var index = 0; index < buttons.Length; index++)
        {
            var button = buttons[index];
            var image = PixelBuffer.Png($"master-4deck-{layout}-deck{index + 1}.png").Image(new ScreenPoint(button.MinX - 4, button.MinY - 4));
            if (SyncMasterDetector.IsMaster(button, image)) masters.Add(index + 1);
        }
        Assert.Equal([2], masters);
    }

    [Fact]
    public void CoversTheLargerTwoDeckVerticalJogDigits()
    {
        var jog = Rect(467, 316, 70, 26);
        var image = PixelBuffer.Png("jog-bpm-2deck-vertical-deck1-135.08.png").Image(new ScreenPoint(457, 309));

        var found = BPMGlyphMask.Glyphs(jog, image);
        var oneDecimal = BPMGlyphMask.Cover(BPMLimit.OneDecimal, jog, image);
        var noDecimals = BPMGlyphMask.Cover(BPMLimit.NoDecimals, jog, image);

        Assert.NotNull(found);
        Assert.Equal(3, found.IntegerDigits.Count);
        Assert.Equal(2, found.Decimals.Count);
        Assert.True(oneDecimal.MinX > found.Decimals[0].MaxX && oneDecimal.MinX <= found.Decimals[1].MinX);
        Assert.True(noDecimals.MinX > found.IntegerDigits[2].MaxX && noDecimals.MinX <= found.Point.MinX);
    }

    [Fact]
    public void PhysicalPixelCoordinatesFindTheSamePlayheadsAndGrids()
    {
        foreach (var frame in new[] { TwoVertical, FourVertical, FourHorizontal })
        {
            var buffer = PixelBuffer.Png(frame.File);
            foreach (var (deck, strip) in frame.Strips)
            {
                var points = PlayheadDetector.Locate(strip, buffer.Image(frame.Origin));
                var physical = PlayheadDetector.Locate(Physical.Strip(strip), buffer.WindowsImage(frame.Origin));
                Assert.True(physical == points * 2, $"{frame.File} deck {deck}: {physical} vs {points}");
                if (points is not { } playhead) continue;

                var grid = BeatGridDetector.Read(strip, playhead, buffer.Image(frame.Origin))!;
                var physicalGrid = BeatGridDetector.Read(Physical.Strip(strip), playhead * 2, buffer.WindowsImage(frame.Origin))!;
                Assert.Equal(grid.BeatXs.Select(x => x * 2), physicalGrid.BeatXs);
                Assert.Equal(grid.PhaseInBar!.Value, physicalGrid.PhaseInBar!.Value, 9);
            }
        }
    }
}
