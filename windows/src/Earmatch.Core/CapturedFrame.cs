using System.Collections.Concurrent;

namespace Earmatch.Core;

public sealed class FramePixels
{
    private readonly PixelPool _pool;
    private int _isReleased;

    public FramePixels(PixelPool pool, byte[] bytes, int width, int height, int stride)
    {
        if (width < 0 || height < 0 || stride < width * 4 || (height > 0 && bytes.Length < (long)stride * (height - 1) + width * 4))
            throw new ArgumentException($"{width}x{height} pixels at {stride} bytes a row do not fit {bytes.Length} bytes");
        _pool = pool;
        Bytes = bytes;
        Width = width;
        Height = height;
        Stride = stride;
    }

    public byte[] Bytes { get; }
    public int Width { get; }
    public int Height { get; }
    public int Stride { get; }

    public PixelImage Image(ScreenPoint origin, double layoutScale) =>
        new(Bytes, Width, Height, Stride, PixelImage.PixelLayout.Bgra, 1, origin, layoutScale);

    public void Release()
    {
        if (Interlocked.Exchange(ref _isReleased, 1) == 0) _pool.Return(Bytes);
    }
}

public sealed class PixelPool
{
    private const int Kept = 4;
    private readonly ConcurrentBag<byte[]> _free = [];

    public byte[] Rent(int length)
    {
        while (_free.TryTake(out var bytes))
        {
            if (bytes.Length == length) return bytes;
        }
        return GC.AllocateUninitializedArray<byte>(length);
    }

    public void Return(byte[] bytes)
    {
        if (_free.Count < Kept) _free.Add(bytes);
    }
}

public readonly record struct PixelRect(int X, int Y, int Width, int Height);

public sealed record CapturedFrame(FramePixels Pixels, ScreenPoint Origin, FrameAnalysis Analysis, int Generation)
{
    public double Time => Analysis.Time;

    public PixelRect PixelRect(ScreenRect rect)
    {
        var left = Math.Clamp((int)Math.Round(rect.MinX - Origin.X), 0, Pixels.Width);
        var top = Math.Clamp((int)Math.Round(rect.MinY - Origin.Y), 0, Pixels.Height);
        var right = Math.Clamp((int)Math.Round(rect.MaxX - Origin.X), left, Pixels.Width);
        var bottom = Math.Clamp((int)Math.Round(rect.MaxY - Origin.Y), top, Pixels.Height);
        return new PixelRect(left, top, right - left, bottom - top);
    }
}

public readonly record struct FramePlacement(ScreenPoint Origin, ScreenSize ExpectedSize)
{
    public static ScreenPoint OriginOf(ScreenRect captured, ScreenRect windowAtFrame, ScreenRect windowAtRead) =>
        new(captured.MinX - (windowAtFrame.MinX - windowAtRead.MinX), captured.MinY - (windowAtFrame.MinY - windowAtRead.MinY));

    public static FramePlacement Of(ScreenRect visibleBounds, ScreenRect windowAtFrame, ScreenRect windowAtRead)
    {
        var bounds = visibleBounds.Width > 0 ? visibleBounds : windowAtFrame;
        var expected = new ScreenSize(windowAtRead.Width - (windowAtFrame.Width - bounds.Width), windowAtRead.Height - (windowAtFrame.Height - bounds.Height));
        return new FramePlacement(OriginOf(bounds, windowAtFrame, windowAtRead), expected);
    }
}

public sealed class FrameHandoff
{
    private readonly object _gate = new();
    private CapturedFrame? _frame;
    private bool _isScheduled;

    public bool Put(CapturedFrame frame)
    {
        CapturedFrame? replaced;
        bool mustSchedule;
        lock (_gate)
        {
            replaced = _frame;
            _frame = frame;
            mustSchedule = !_isScheduled;
            _isScheduled = true;
        }
        replaced?.Pixels.Release();
        return mustSchedule;
    }

    public CapturedFrame? Take()
    {
        lock (_gate)
        {
            _isScheduled = false;
            var taken = _frame;
            _frame = null;
            return taken;
        }
    }

    public void Discard(double capturedBefore)
    {
        CapturedFrame? stale = null;
        lock (_gate)
        {
            if (_frame is { } frame && frame.Time < capturedBefore)
            {
                stale = frame;
                _frame = null;
            }
        }
        stale?.Pixels.Release();
    }
}
