using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class PixelImageTests
{
    static readonly byte[] Bytes =
    [
        10, 20, 30, 255, 40, 50, 60, 255, 0, 0, 0, 0,
        70, 80, 90, 255, 100, 110, 120, 255, 0, 0, 0, 0,
    ];

    [Fact]
    public void BGRAPixelsReadTheirChannelsBackwards()
    {
        var image = new PixelImage(Bytes, width: 2, height: 2, bytesPerRow: 12, PixelImage.PixelLayout.Bgra, scale: 1, origin: new ScreenPoint(0, 0));

        Assert.Equal(new PixelImage.RGB(R: 30, G: 20, B: 10), image.Pixel(0, 0));
        Assert.Equal(new PixelImage.RGB(R: 120, G: 110, B: 100), image.Pixel(1, 1));
    }

    [Fact]
    public void RGBAPixelsReadTheirChannelsInOrder()
    {
        var image = new PixelImage(Bytes, width: 2, height: 2, bytesPerRow: 12, PixelImage.PixelLayout.Rgba, scale: 1, origin: new ScreenPoint(0, 0));

        Assert.Equal(new PixelImage.RGB(R: 40, G: 50, B: 60), image.Pixel(1, 0));
        Assert.Equal(new PixelImage.RGB(R: 70, G: 80, B: 90), image.Pixel(0, 1));
    }

    [Fact]
    public void ScreenCoordinatesMapToClampedColumnsAndRowsAndBack()
    {
        var image = new PixelImage(new byte[16 * 4 * 8], width: 16, height: 8, bytesPerRow: 64, PixelImage.PixelLayout.Bgra, scale: 2,
            origin: new ScreenPoint(100, 50));

        Assert.Equal(5, image.Column(102.7));
        Assert.Equal(0, image.Column(90));
        Assert.Equal(16, image.Column(200));
        Assert.Equal(3, image.Row(51.5));
        Assert.Equal(8, image.Row(80));
        Assert.Equal(103.5, image.PointX(7));
        Assert.Equal(52, image.PointY(4));
    }
}
