using Earmatch.Core;
using static Earmatch.Core.Tests.Fixture;

namespace Earmatch.Core.Tests;

public class BeatGridDetectorTests
{
    static readonly ScreenPoint CropOrigin = new(0, 136);
    static readonly ScreenRect Deck1Strip = Rect(0, 136, 1492, 69);
    static readonly ScreenRect Deck2Strip = Rect(0, 205, 1492, 69);
    const double PlayheadX = 746;
    static readonly (byte, byte, byte) Grey = (124, 124, 124);
    static readonly (byte, byte, byte) Red = (255, 0, 0);
    static readonly ScreenRect SyntheticStrip = Rect(0, 0, 600, 60);

    static PixelBuffer MakeSyntheticStrip()
    {
        var buffer = new PixelBuffer(1200, 120);
        for (var beat = 0; beat < 16; beat++)
        {
            var x = (100 + beat * 30) * 2;
            if (beat % 4 == 1)
            {
                buffer.Paint(x - 5, 2, 11, 6, Red);
            }
            else
            {
                buffer.Paint(x - 2, 2, 5, 2, Grey);
                buffer.Paint(x, 2, 1, 6, Grey);
            }
        }
        buffer.Paint(600, 0, 2, 120, (112, 112, 112));
        return buffer;
    }

    [Fact]
    public void ReadsSpacingAndPhaseFromSyntheticMarkers()
    {
        var reading = BeatGridDetector.Read(SyntheticStrip, 300, MakeSyntheticStrip().Image());

        Assert.NotNull(reading);
        Assert.True(Math.Abs(reading.BeatSpacing - 30) < 0.1);
        Assert.Equal(4, reading.DownbeatXs.Count);
        Assert.True(Math.Abs(reading.PhaseInBar!.Value - 50.0 / 30.0) < 0.02);
    }

    [Fact]
    public void ThePlayheadIsNotABeat()
    {
        var reading = BeatGridDetector.Read(SyntheticStrip, 300, MakeSyntheticStrip().Image());

        Assert.NotNull(reading);
        Assert.DoesNotContain(reading.BeatXs, x => Math.Abs(x - 300) < 2);
    }

    [Fact]
    public void AnOffGridMarkerNearThePlayheadDoesNotBecomeTheGridReference()
    {
        var buffer = MakeSyntheticStrip();
        buffer.Paint(590, 2, 5, 2, Grey);
        buffer.Paint(592, 2, 1, 6, Grey);

        var reading = BeatGridDetector.Read(SyntheticStrip, 300, buffer.Image());

        Assert.NotNull(reading);
        Assert.True(Math.Abs(reading.BeatSpacing - 30) < 0.1);
        Assert.DoesNotContain(reading.BeatXs, x => Math.Abs(x - 296) < 2);
        Assert.True(Math.Abs(reading.PhaseInBar!.Value - 50.0 / 30.0) < 0.02);
    }

    [Fact]
    public void TheWaveformsColourModeDoesNotChangeTheReading()
    {
        var plain = BeatGridDetector.Read(SyntheticStrip, 300, MakeSyntheticStrip().Image());
        (byte, byte, byte)[] colours = [(230, 40, 30), (240, 240, 240), (40, 90, 230), (240, 150, 40), (124, 124, 124)];
        var buffer = MakeSyntheticStrip();
        for (var column = 0; column < 1200; column += 6)
        {
            var height = 20 + column * 7 % 70;
            buffer.Paint(column, 60 - height / 2, 6, height, colours[column / 6 % colours.Length]);
        }

        var coloured = BeatGridDetector.Read(SyntheticStrip, 300, buffer.Image());

        Assert.NotNull(plain);
        Assert.Equal(plain, coloured);
    }

    [Fact]
    public void AnEmptyStripHasNoReading()
    {
        Assert.Null(BeatGridDetector.Read(SyntheticStrip, 300, new PixelBuffer(1200, 120).Image()));
    }

    [Fact]
    public void MatchesTheBarCountersRekordboxDrawsOnScreen()
    {
        var image = PixelBuffer.Png("waveform-playing-2.png").Image(CropOrigin);

        var deck1 = BeatGridDetector.Read(Deck1Strip, PlayheadX, image);
        var deck2 = BeatGridDetector.Read(Deck2Strip, PlayheadX, image);

        Assert.NotNull(deck1);
        Assert.NotNull(deck2);
        Assert.True(Math.Abs(deck1.BeatSpacing - 27.75) < 0.4, $"{deck1.BeatSpacing}");
        Assert.True(Math.Abs(deck2.BeatSpacing - 30.0) < 0.4, $"{deck2.BeatSpacing}");
        Assert.Equal(2, (int)deck1.PhaseInBar!.Value);
        Assert.Equal(1, (int)deck2.PhaseInBar!.Value);
    }

    [Fact]
    public void PhaseAdvancesInProportionToEachDecksTempo()
    {
        static (double Phase1, double Phase2, double Spacing1, double Spacing2) Phases(string name)
        {
            var image = PixelBuffer.Png(name).Image(CropOrigin);
            var d1 = BeatGridDetector.Read(Deck1Strip, PlayheadX, image)!;
            var d2 = BeatGridDetector.Read(Deck2Strip, PlayheadX, image)!;
            return (d1.PhaseInBar!.Value, d2.PhaseInBar!.Value, d1.BeatSpacing, d2.BeatSpacing);
        }
        static double Advance(double from, double to) => (to - from + 4) % 4;

        var start = Phases("waveform-playing-0.png");
        var end = Phases("waveform-playing-4.png");
        var advance1 = Advance(start.Phase1, end.Phase1);
        var advance2 = Advance(start.Phase2, end.Phase2);

        Assert.True(Math.Abs(advance1 - 0.805 * 135 / 60) < 0.2, $"{advance1}");
        Assert.True(Math.Abs(advance1 / advance2 - 135.0 / 124.0) < 0.04, $"{advance1 / advance2}");
        Assert.True(Math.Abs(start.Spacing2 / start.Spacing1 - 135.0 / 124.0) < 0.02, $"{start.Spacing2 / start.Spacing1}");
    }

    [Fact]
    public void SpotsTheSyncMasterByItsBlueBorder()
    {
        var image = PixelBuffer.Png("waveform-playing-2.png").Image(CropOrigin);

        Assert.True(SyncMasterDetector.IsMaster(Rect(612, 311, 70, 13), image));
        Assert.False(SyncMasterDetector.IsMaster(Rect(1414, 311, 70, 13), image));
    }

    [Fact]
    public void ThereAreFourBeatsInABar()
    {
        Assert.Equal(4.0, BeatGridDetector.BeatsPerBar);
    }

    /// rekordbox for Windows at 200% hands over the same pixels with screen units in physical pixels: every position
    /// and spacing doubles, the phase stays.
    [Fact]
    public void PhysicalPixelCoordinatesReadTheSameGrid()
    {
        var buffer = PixelBuffer.Png("waveform-playing-2.png");

        foreach (var strip in new[] { Deck1Strip, Deck2Strip })
        {
            var points = BeatGridDetector.Read(strip, PlayheadX, buffer.Image(CropOrigin))!;
            var physical = BeatGridDetector.Read(Physical.Rect(strip), PlayheadX * 2, buffer.WindowsImage(CropOrigin))!;

            Assert.Equal(points.BeatXs.Select(x => x * 2), physical.BeatXs);
            Assert.Equal(points.DownbeatXs.Select(x => x * 2), physical.DownbeatXs);
            Assert.Equal(points.BeatSpacing * 2, physical.BeatSpacing, 9);
            Assert.Equal(points.PhaseInBar!.Value, physical.PhaseInBar!.Value, 9);
        }
        Assert.True(SyncMasterDetector.IsMaster(Physical.Rect(Rect(612, 311, 70, 13)), buffer.WindowsImage(CropOrigin)));
        Assert.False(SyncMasterDetector.IsMaster(Physical.Rect(Rect(1414, 311, 70, 13)), buffer.WindowsImage(CropOrigin)));
    }

    /// A MASTER button outside the frame (the window partly off screen, or a frame that does not match the layout) is
    /// not read from the frame's edge rows or columns: only its rows and columns inside the frame count.
    [Fact]
    public void TheSyncMasterProbeReadsOnlyThePartOfTheButtonInsideTheFrame()
    {
        var buffer = new PixelBuffer(200, 40, (20, 20, 20));
        buffer.Paint(0, 0, 200, 1, (40, 120, 230));
        buffer.Paint(0, 39, 200, 1, (40, 120, 230));
        var image = buffer.Image();

        Assert.False(SyncMasterDetector.IsMaster(Rect(10, -30, 70, 13), image));
        Assert.False(SyncMasterDetector.IsMaster(Rect(10, 25, 70, 13), image));
        Assert.False(SyncMasterDetector.IsMaster(Rect(150, 5, 70, 13), image));
        Assert.True(SyncMasterDetector.IsMaster(Rect(10, 0, 70, 13), image));
    }
}
