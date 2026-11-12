using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class CapturedFrameTests
{
    static CapturedFrame Frame(PixelPool pool, double time, int width = 4, int height = 3, ScreenPoint origin = default) =>
        new(new FramePixels(pool, new byte[width * 4 * height], width, height, width * 4), origin,
            new FrameAnalysis(new Dictionary<int, DeckFrameReading>(), null, time), Generation: 1);

    static ScreenRect Rect(double x, double y, double width, double height) => new(x, y, width, height);

    [Fact]
    public void ANewerFrameReplacesAnUntakenOneAndGivesItsPixelsBack()
    {
        var pool = new PixelPool();
        var handoff = new FrameHandoff();
        var first = Frame(pool, 1);
        var second = Frame(pool, 2);

        Assert.True(handoff.Put(first));
        Assert.False(handoff.Put(second));

        Assert.Same(second, handoff.Take());
        Assert.Null(handoff.Take());
        Assert.Same(first.Pixels.Bytes, pool.Rent(first.Pixels.Bytes.Length));
    }

    [Fact]
    public void TheFirstFrameAfterATakeSchedulesADeliveryAgain()
    {
        var pool = new PixelPool();
        var handoff = new FrameHandoff();
        handoff.Put(Frame(pool, 1));
        handoff.Take();

        Assert.True(handoff.Put(Frame(pool, 2)));
    }

    [Fact]
    public void DiscardingDropsOnlyAFrameCapturedBeforeTheCutoff()
    {
        var pool = new PixelPool();
        var handoff = new FrameHandoff();
        var fresh = Frame(pool, 5);
        handoff.Put(fresh);

        handoff.Discard(capturedBefore: 5);
        Assert.Same(fresh, handoff.Take());

        var stale = Frame(pool, 4);
        handoff.Put(stale);
        handoff.Discard(capturedBefore: 5);
        Assert.Null(handoff.Take());
        Assert.Same(stale.Pixels.Bytes, pool.Rent(stale.Pixels.Bytes.Length));
    }

    [Fact]
    public void PixelsGoBackToThePoolOnceAndABufferIsReusedOnlyForTheSameLength()
    {
        var pool = new PixelPool();
        var bytes = pool.Rent(48);
        var pixels = new FramePixels(pool, bytes, 4, 3, 16);

        pixels.Release();
        pixels.Release();

        Assert.Same(bytes, pool.Rent(48));
        Assert.NotSame(bytes, pool.Rent(48));
        pool.Return(bytes);
        Assert.Equal(64, pool.Rent(64).Length);
    }

    [Fact]
    public void AFrameHoldsOnlyThePixelsItsBufferCanHold()
    {
        var pool = new PixelPool();
        var bytes = new byte[100 * 4 * 50];

        var copied = new FramePixels(pool, bytes, 60, 30, 400);
        var image = copied.Image(new ScreenPoint(10, 20), 2);

        Assert.Equal((60, 30, 400), (image.Width, image.Height, image.BytesPerRow));
        Assert.Equal(new ScreenPoint(10, 20), image.Origin);
        Assert.Equal(2, image.ScreenUnitsPerPoint);
        Assert.Throws<ArgumentException>(() => new FramePixels(pool, bytes, 100, 51, 400));
        Assert.Throws<ArgumentException>(() => new FramePixels(pool, bytes, 101, 10, 400));
    }

    [Fact]
    public void APixelRectangleIsClippedToTheFrame()
    {
        var frame = Frame(new PixelPool(), 1, width: 100, height: 50, origin: new ScreenPoint(1000, 500));

        Assert.Equal(new PixelRect(10, 5, 20, 10), frame.PixelRect(Rect(1010, 505, 20, 10)));
        Assert.Equal(new PixelRect(0, 0, 10, 5), frame.PixelRect(Rect(990, 495, 20, 10)));
        Assert.Equal(new PixelRect(90, 45, 10, 5), frame.PixelRect(Rect(1090, 545, 20, 10)));
        Assert.Equal(0, frame.PixelRect(Rect(2000, 2000, 20, 10)).Width);
    }

    [Fact]
    public void AFrameIsPlacedAtTheVisibleBoundsWhereTheWindowWasRead()
    {
        var window = Rect(100, 200, 1616, 1239);
        var visible = Rect(108, 200, 1600, 1231);

        var still = FramePlacement.Of(visible, window, window);
        Assert.Equal(new ScreenPoint(108, 200), still.Origin);
        Assert.Equal(new ScreenSize(1600, 1231), still.ExpectedSize);

        var moved = FramePlacement.Of(visible with { X = 158, Y = 170 }, window with { X = 150, Y = 170 }, window);
        Assert.Equal(new ScreenPoint(108, 200), moved.Origin);

        var resized = FramePlacement.Of(visible with { Width = 1400 }, window with { Width = 1416 }, window);
        Assert.Equal(new ScreenSize(1600, 1231), resized.ExpectedSize);
    }

    [Fact]
    public void WithoutVisibleBoundsTheFrameIsPlacedAtTheWindowRectangle()
    {
        var window = Rect(100, 200, 1616, 1239);

        var placement = FramePlacement.Of(default, window, window with { X = 90 });

        Assert.Equal(new ScreenPoint(90, 200), placement.Origin);
        Assert.Equal(new ScreenSize(1616, 1239), placement.ExpectedSize);
        Assert.Equal(new ScreenPoint(98, 200), FramePlacement.OriginOf(Rect(108, 200, 1600, 1231), window, window with { X = 90 }));
    }
}
