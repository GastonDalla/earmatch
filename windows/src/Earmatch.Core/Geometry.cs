namespace Earmatch.Core;

/// A point in screen coordinates (physical pixels on Windows).
public readonly record struct ScreenPoint(double X, double Y);

/// A rectangle in screen coordinates (physical pixels on Windows), with the semantics of CoreGraphics' CGRect that the
/// macOS code relies on: Contains includes the min edges and excludes the max edges.
public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public double MinX => X;
    public double MidX => X + Width / 2;
    public double MaxX => X + Width;
    public double MinY => Y;
    public double MidY => Y + Height / 2;
    public double MaxY => Y + Height;
    public ScreenPoint Origin => new(X, Y);
    public ScreenPoint Center => new(MidX, MidY);
    public double Area => Width * Height;

    public bool Contains(ScreenPoint point) => point.X >= MinX && point.X < MaxX && point.Y >= MinY && point.Y < MaxY;

    public ScreenRect Union(ScreenRect other)
    {
        var minX = Math.Min(MinX, other.MinX);
        var minY = Math.Min(MinY, other.MinY);
        return new ScreenRect(minX, minY, Math.Max(MaxX, other.MaxX) - minX, Math.Max(MaxY, other.MaxY) - minY);
    }
}
