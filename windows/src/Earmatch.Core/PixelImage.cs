namespace Earmatch.Core;

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

    public PixelImage(ReadOnlyMemory<byte> bytes, int width, int height, int bytesPerRow, PixelLayout layout, double pixelsPerScreenUnit,
        ScreenPoint origin, double screenUnitsPerPoint = 1)
    {
        _bytes = bytes;
        Width = width;
        Height = height;
        BytesPerRow = bytesPerRow;
        Layout = layout;
        PixelsPerScreenUnit = pixelsPerScreenUnit;
        Origin = origin;
        ScreenUnitsPerPoint = screenUnitsPerPoint;
    }

    public int Width { get; }
    public int Height { get; }
    public int BytesPerRow { get; }
    public PixelLayout Layout { get; }
    public double PixelsPerScreenUnit { get; }
    public ScreenPoint Origin { get; }
    public double ScreenUnitsPerPoint { get; }

    public double PixelsPerPoint => PixelsPerScreenUnit * ScreenUnitsPerPoint;

    public double PointsInScreenUnits(double points) => points * ScreenUnitsPerPoint;

    public RGB Pixel(int x, int y)
    {
        System.Diagnostics.Debug.Assert(x >= 0 && x < Width && y >= 0 && y < Height, $"pixel ({x}, {y}) outside {Width}x{Height}");
        var p = _bytes.Span.Slice(y * BytesPerRow + x * 4, 4);
        return Layout == PixelLayout.Bgra ? new RGB(p[2], p[1], p[0]) : new RGB(p[0], p[1], p[2]);
    }

    public int Column(double x) => Clamp(UnclampedColumn(x), Width);

    public int UnclampedColumn(double x) => (int)Math.Floor((x - Origin.X) * PixelsPerScreenUnit);

    public int UnclampedRow(double y) => (int)Math.Floor((y - Origin.Y) * PixelsPerScreenUnit);

    public int Row(double y) => Clamp(UnclampedRow(y), Height);
    public double ScreenX(double column) => Origin.X + column / PixelsPerScreenUnit;
    public double ScreenY(double row) => Origin.Y + row / PixelsPerScreenUnit;

    private static int Clamp(int value, int limit) => Math.Min(Math.Max(value, 0), limit);
}
