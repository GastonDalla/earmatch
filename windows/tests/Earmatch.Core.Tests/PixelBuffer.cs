using Earmatch.Core;

namespace Earmatch.Core.Tests;

sealed class PixelBuffer
{
    public PixelBuffer(int width, int height, (byte R, byte G, byte B) fill = default)
    {
        Width = width;
        Height = height;
        Bytes = new byte[width * height * 4];
        for (var index = 0; index < width * height; index++)
        {
            Bytes[index * 4] = fill.R;
            Bytes[index * 4 + 1] = fill.G;
            Bytes[index * 4 + 2] = fill.B;
            Bytes[index * 4 + 3] = 255;
        }
    }

    private PixelBuffer(int width, int height, byte[] bytes)
    {
        Width = width;
        Height = height;
        Bytes = bytes;
    }

    public static PixelBuffer Png(string name)
    {
        var (width, height, rgba) = Tests.Png.Decode(File.ReadAllBytes(Fixture.PathOf(name)));
        return new PixelBuffer(width, height, rgba);
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Bytes { get; }

    public void Paint(int x, int y, int width, int height, (byte R, byte G, byte B) color)
    {
        for (var row = Math.Max(0, y); row < Math.Min(Height, y + height); row++)
        {
            for (var column = Math.Max(0, x); column < Math.Min(Width, x + width); column++)
            {
                var index = (row * Width + column) * 4;
                Bytes[index] = color.R;
                Bytes[index + 1] = color.G;
                Bytes[index + 2] = color.B;
            }
        }
    }

    public PixelImage Image(ScreenPoint origin = default, double scale = Fixture.VmDisplayAt200Percent) =>
        new(Bytes, Width, Height, Width * 4, PixelImage.PixelLayout.Rgba, pixelsPerScreenUnit: scale, origin);

    public PixelImage WindowsImage(ScreenPoint pointOrigin = default) =>
        new(Bytes, Width, Height, Width * 4, PixelImage.PixelLayout.Rgba, pixelsPerScreenUnit: 1, Physical.Point(pointOrigin),
            screenUnitsPerPoint: Physical.PixelsPerPoint);
}

static class Physical
{
    public const double PixelsPerPoint = Fixture.VmDisplayAt200Percent;

    public static ScreenPoint Point(ScreenPoint point) => new(point.X * PixelsPerPoint, point.Y * PixelsPerPoint);

    public static ScreenRect Rect(ScreenRect rect) =>
        new(rect.X * PixelsPerPoint, rect.Y * PixelsPerPoint, rect.Width * PixelsPerPoint, rect.Height * PixelsPerPoint);

    public static WaveformStrip Strip(WaveformStrip strip) => new(Rect(strip.Frame), strip.Orientation, Rect(strip.MarkerArea));
}
