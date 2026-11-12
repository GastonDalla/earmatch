using System.Collections.Concurrent;
using Earmatch.Core;

namespace Earmatch.App.Capture;

/// The BGRA pixels of one captured frame of rekordbox's window, `Stride` bytes a row, in a buffer lent by a pool.
/// Whoever holds the frame last gives it back (Release): the hand-off when it drops an untaken frame, the overlays
/// when a newer frame replaces it.
internal sealed class FramePixels
{
    private readonly PixelPool _pool;
    private int _isReleased;

    public FramePixels(PixelPool pool, byte[] bytes, int width, int height, int stride)
    {
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

    public void Release()
    {
        if (Interlocked.Exchange(ref _isReleased, 1) == 0) _pool.Return(Bytes);
    }
}

/// A few frame-sized buffers reused frame after frame: a 2048x1400 window is 11 MB of pixels, sixty times a second.
internal sealed class PixelPool
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

/// A frame read by the analyser (FrameReading on macOS): its pixels, the screen point of its pixel (0, 0) in the
/// layout's coordinates, what the analyser read, and the capture generation it belongs to.
internal sealed record CapturedFrame(FramePixels Pixels, ScreenPoint Origin, FrameAnalysis Analysis, int Generation)
{
    public double Time => Analysis.Time;

    /// The pixel rectangle of a rectangle in layout coordinates, clipped to the frame.
    public System.Windows.Int32Rect PixelRect(ScreenRect rect)
    {
        var left = Math.Clamp((int)Math.Round(rect.MinX - Origin.X), 0, Pixels.Width);
        var top = Math.Clamp((int)Math.Round(rect.MinY - Origin.Y), 0, Pixels.Height);
        var right = Math.Clamp((int)Math.Round(rect.MaxX - Origin.X), left, Pixels.Width);
        var bottom = Math.Clamp((int)Math.Round(rect.MaxY - Origin.Y), top, Pixels.Height);
        return new System.Windows.Int32Rect(left, top, right - left, bottom - top);
    }
}

/// The newest analysed frame on its way from the capture thread to the window's thread (FrameHandoff in
/// OverlayController.swift): a newer frame replaces an untaken one, and only the first frame after a take schedules a
/// delivery.
internal sealed class FrameHandoff
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

    /// Drops an untaken frame captured before `time`.
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
