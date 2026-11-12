using System.Collections.Concurrent;

namespace Earmatch.Core;

/// The BGRA pixels of one captured frame of rekordbox's window, `Stride` bytes a row, in a buffer lent by a pool.
/// Width and Height are the pixels actually captured; the buffer may be larger. Whoever holds the frame last gives it
/// back (Release): the hand-off when it drops an untaken frame, the overlays when a newer frame replaces it.
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

    /// The pixels as the analyser reads them, with pixel (0, 0) at `origin` in the layout's coordinates.
    public PixelImage Image(ScreenPoint origin, double layoutScale) =>
        new(Bytes, Width, Height, Stride, PixelImage.PixelLayout.Bgra, 1, origin, layoutScale);

    public void Release()
    {
        if (Interlocked.Exchange(ref _isReleased, 1) == 0) _pool.Return(Bytes);
    }
}

/// A few frame-sized buffers reused frame after frame: a 2048x1400 window is 11 MB of pixels, sixty times a second.
/// A rented buffer is not cleared: only the pixels a frame captured may be read (FramePixels' Width and Height).
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

/// A rectangle of whole pixels in a captured frame.
public readonly record struct PixelRect(int X, int Y, int Width, int Height);

/// A frame read by the analyser (FrameReading on macOS): its pixels, the screen point of its pixel (0, 0) in the
/// layout's coordinates, what the analyser read, and the capture generation it belongs to.
public sealed record CapturedFrame(FramePixels Pixels, ScreenPoint Origin, FrameAnalysis Analysis, int Generation)
{
    public double Time => Analysis.Time;

    /// The pixel rectangle of a rectangle in layout coordinates, clipped to the frame.
    public PixelRect PixelRect(ScreenRect rect)
    {
        var left = Math.Clamp((int)Math.Round(rect.MinX - Origin.X), 0, Pixels.Width);
        var top = Math.Clamp((int)Math.Round(rect.MinY - Origin.Y), 0, Pixels.Height);
        var right = Math.Clamp((int)Math.Round(rect.MaxX - Origin.X), left, Pixels.Width);
        var bottom = Math.Clamp((int)Math.Round(rect.MaxY - Origin.Y), top, Pixels.Height);
        return new PixelRect(left, top, right - left, bottom - top);
    }
}

/// Where a captured frame sits in the layout's coordinates. Windows.Graphics.Capture captures a window's visible bounds
/// (DWMWA_EXTENDED_FRAME_BOUNDS: the window without its invisible resize borders, title bar included), so a frame's
/// pixel (0, 0) is at those bounds' top-left corner. The layout was read with the window at another place, perhaps:
/// the frame is placed where it would have been then, and must have the size of the visible bounds of the window read.
public readonly record struct FramePlacement(ScreenPoint Origin, ScreenSize ExpectedSize)
{
    /// The layout point of a frame's pixel (0, 0) when that pixel was the top-left corner of `captured`, a rectangle on
    /// screen when the window's rectangle was `windowAtFrame`; the layout was read with the window at `windowAtRead`.
    public static ScreenPoint OriginOf(ScreenRect captured, ScreenRect windowAtFrame, ScreenRect windowAtRead) =>
        new(captured.MinX - (windowAtFrame.MinX - windowAtRead.MinX), captured.MinY - (windowAtFrame.MinY - windowAtRead.MinY));

    /// A frame of the window's visible bounds (its window rectangle when the bounds are unknown, empty): its origin, and
    /// the size it has when it shows the window the layout was read from (the bounds keep their margins in the window).
    public static FramePlacement Of(ScreenRect visibleBounds, ScreenRect windowAtFrame, ScreenRect windowAtRead)
    {
        var bounds = visibleBounds.Width > 0 ? visibleBounds : windowAtFrame;
        var expected = new ScreenSize(windowAtRead.Width - (windowAtFrame.Width - bounds.Width), windowAtRead.Height - (windowAtFrame.Height - bounds.Height));
        return new FramePlacement(OriginOf(bounds, windowAtFrame, windowAtRead), expected);
    }
}

/// The newest analysed frame on its way from the capture thread to the window's thread (FrameHandoff in
/// OverlayController.swift): a newer frame replaces an untaken one, and only the first frame after a take schedules a
/// delivery. A frame it drops gives its pixels back.
public sealed class FrameHandoff
{
    private readonly object _gate = new();
    private CapturedFrame? _frame;
    private bool _isScheduled;

    /// Leaves a frame; true when the window's thread must be asked to take it.
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

    /// Drops an untaken frame captured before `capturedBefore`.
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
