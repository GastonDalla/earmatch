using Earmatch.Core;

namespace Earmatch.Core.Tests;

/// RGBA pixels to paint or load from a PNG fixture (PixelFixture.swift on macOS). The fixtures are rekordbox frames at
/// 2 pixels per point.
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

    /// The pixels in macOS points: 2 pixels per screen unit, one screen unit per rekordbox point.
    public PixelImage Image(ScreenPoint origin = default, double scale = 2) =>
        new(Bytes, Width, Height, Width * 4, PixelImage.PixelLayout.Rgba, scale, origin);

    /// The same pixels as rekordbox for Windows at 200% gives them: screen units are physical pixels, two per point.
    public PixelImage WindowsImage(ScreenPoint pointOrigin = default) =>
        new(Bytes, Width, Height, Width * 4, PixelImage.PixelLayout.Rgba, scale: 1, Physical.Point(pointOrigin), layoutScale: 2);
}

/// Converts macOS point geometry to the physical pixels rekordbox for Windows uses at 200%.
static class Physical
{
    public const double Scale = 2;

    public static ScreenPoint Point(ScreenPoint point) => new(point.X * Scale, point.Y * Scale);

    public static ScreenRect Rect(ScreenRect rect) => new(rect.X * Scale, rect.Y * Scale, rect.Width * Scale, rect.Height * Scale);

    public static WaveformStrip Strip(WaveformStrip strip) => new(Rect(strip.Frame), strip.Orientation, Rect(strip.MarkerArea));
}
