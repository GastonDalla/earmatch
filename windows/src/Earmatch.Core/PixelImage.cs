namespace Earmatch.Core;

/// A captured frame of the rekordbox window: 4 bytes per pixel, rows of BytesPerRow bytes. Origin is the screen point
/// of pixel (0, 0) and Scale the pixels per screen unit.
public readonly struct PixelImage
{
    public enum PixelLayout
    {
        Bgra,
        Rgba,
    }

    public readonly record struct RGB(byte R, byte G, byte B)
    {
        internal bool IsMarkerRed => R >= 180 && G <= 90 && B <= 90;

        internal bool IsTickGrey => Math.Abs(R - G) <= 14 && Math.Abs(G - B) <= 14 && R is >= 95 and <= 215;

        internal bool IsPlayheadGrey => Math.Abs(R - G) <= 6 && Math.Abs(G - B) <= 6 && R is >= 100 and <= 124;

        internal bool IsMasterBlue => B >= 170 && R <= 100 && G is >= 60 and <= 180;
    }

    private readonly ReadOnlyMemory<byte> _bytes;

    public PixelImage(ReadOnlyMemory<byte> bytes, int width, int height, int bytesPerRow, PixelLayout layout, double scale, ScreenPoint origin)
    {
        _bytes = bytes;
        Width = width;
        Height = height;
        BytesPerRow = bytesPerRow;
        Layout = layout;
        Scale = scale;
        Origin = origin;
    }

    public int Width { get; }
    public int Height { get; }
    public int BytesPerRow { get; }
    public PixelLayout Layout { get; }
    public double Scale { get; }
    public ScreenPoint Origin { get; }

    public RGB Pixel(int x, int y)
    {
        System.Diagnostics.Debug.Assert(x >= 0 && x < Width && y >= 0 && y < Height, $"pixel ({x}, {y}) outside {Width}x{Height}");
        var p = _bytes.Span.Slice(y * BytesPerRow + x * 4, 4);
        return Layout == PixelLayout.Bgra ? new RGB(p[2], p[1], p[0]) : new RGB(p[0], p[1], p[2]);
    }

    public int Column(double x) => Clamp((int)Math.Floor((x - Origin.X) * Scale), Width);
    public int Row(double y) => Clamp((int)Math.Floor((y - Origin.Y) * Scale), Height);
    public double PointX(double column) => Origin.X + column / Scale;
    public double PointY(double row) => Origin.Y + row / Scale;

    private static int Clamp(int value, int limit) => Math.Min(Math.Max(value, 0), limit);
}
