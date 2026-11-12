using System.Diagnostics;
using System.Windows.Threading;
using Earmatch.Core;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace Earmatch.App.Capture;

internal enum CaptureStatusKind
{
    Stopped,
    Starting,
    WaitingForWindow,
    Running,
    Failed,
}

/// Where the capture stands (WindowCapture.Status on macOS). Windows asks no permission to capture a window, so the
/// macOS permission state is gone; a capture Windows refuses (an elevated rekordbox) fails with its reason.
internal readonly record struct CaptureStatus(CaptureStatusKind Kind, string? Reason = null)
{
    public static CaptureStatus Stopped => new(CaptureStatusKind.Stopped);

    public bool IsRunning => Kind == CaptureStatusKind.Running;

    public string Message => Kind switch
    {
        CaptureStatusKind.Stopped => L10n.Text("Capture stopped"),
        CaptureStatusKind.Starting => L10n.Text("Starting capture of the rekordbox window"),
        CaptureStatusKind.WaitingForWindow => L10n.Text("Waiting for the rekordbox window to be on screen"),
        CaptureStatusKind.Running => L10n.Text("Capturing the rekordbox window"),
        _ => L10n.Format("Capture failed: %@", Reason ?? ""),
    };
}

/// One captured frame as it leaves the capture thread: its pixels, when it was captured (seconds on CaptureClock), the
/// window's visible bounds and window rectangle at that moment (physical pixels), and its capture generation.
internal readonly record struct RawFrame(FramePixels Pixels, double Time, NativeMethods.WindowRect Bounds, NativeMethods.WindowRect WindowRect,
    int Generation);

/// The performance counter in seconds: the clock of Windows.Graphics.Capture's frame times and of the screen refreshes.
internal static class CaptureClock
{
    public static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    public static double Seconds(long ticks) => ticks / (double)Stopwatch.Frequency;
}

/// Captures rekordbox's main window with Windows.Graphics.Capture (WindowCapture.swift on macOS, ScreenCaptureKit):
/// a free-threaded Direct3D 11 frame pool delivers each new frame on a worker thread, where its pixels are copied to
/// the CPU (BGRA) for the analyser; the yellow capture border is turned off where Windows allows it, and the cursor is
/// left out. It runs only while a feature needs pixels, follows the window it is given, waits while the window is
/// minimized or hidden, and retries a failed start on RetrySchedule's delays. It only reads the window's pixels.
/// Follow, Stop and the status are used on the window's thread.
internal sealed class WindowCapture : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly RetrySchedule _schedule = new();
    private readonly PixelPool _pixels = new();
    private readonly object _frameGate = new();
    private IntPtr _wanted;
    private Direct3D? _direct3D;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _pool;
    private GraphicsCaptureSession? _session;
    private SizeInt32 _poolSize;
    private int _deliveringGeneration;
    private DispatcherTimer? _retryTimer;
    private static bool _askedForBorderless;

    public WindowCapture(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// Raised on the window's thread when the status changed.
    public event Action<CaptureStatus>? StatusChanged;

    /// Called on the capture thread with each new frame; the receiver owns its pixels.
    public Action<RawFrame>? FrameArrived { get; set; }

    public CaptureStatus Status { get; private set; } = CaptureStatus.Stopped;

    /// Grows at each start and stop: frames of an earlier generation are stale.
    public int Generation { get; private set; }

    /// The window captured, or zero.
    public IntPtr Window => _wanted;

    /// Captures `window` (zero stops the capture). A window already being captured keeps its stream; a failed start is
    /// tried again once its retry delay has passed.
    public void Follow(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            if (_wanted != IntPtr.Zero || Status.Kind != CaptureStatusKind.Stopped) Stop();
            return;
        }
        var sameWindow = _wanted == window;
        _wanted = window;
        switch (Status.Kind)
        {
            case CaptureStatusKind.Running or CaptureStatusKind.Starting or CaptureStatusKind.WaitingForWindow when sameWindow:
                return;
            case CaptureStatusKind.Failed when sameWindow:
                if (_schedule.AllowsAttempt(CaptureClock.Now)) Start();
                return;
            default:
                Start();
                return;
        }
    }

    /// rekordbox came back in front: a capture waiting for its window tries at once.
    public void WindowMayBeBack()
    {
        if (Status.Kind != CaptureStatusKind.WaitingForWindow || _wanted == IntPtr.Zero) return;
        _schedule.Reset();
        Start();
    }

    public void Stop()
    {
        _wanted = IntPtr.Zero;
        _schedule.Reset();
        Generation++;
        TearDown();
        SetStatus(CaptureStatus.Stopped);
    }

    private void Start()
    {
        if (_wanted == IntPtr.Zero) return;
        _schedule.AttemptStarted();
        Generation++;
        var generation = Generation;
        TearDown();
        if (Status.Kind != CaptureStatusKind.WaitingForWindow) SetStatus(new CaptureStatus(CaptureStatusKind.Starting));
        if (!NativeMethods.IsWindow(_wanted))
        {
            Fail(L10n.Text("rekordbox window not found"));
            return;
        }
        if (NativeMethods.IsIconic(_wanted) || !NativeMethods.IsShown(_wanted))
        {
            Retry(RetrySchedule.Reason.WindowOffScreen, new CaptureStatus(CaptureStatusKind.WaitingForWindow));
            return;
        }
        _schedule.WindowFound();
        try
        {
            if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("Windows.Graphics.Capture is not available");
            _direct3D ??= Direct3D.Create();
            var item = Direct3D.ItemForWindow(_wanted);
            item.Closed += (_, _) => _dispatcher.BeginInvoke(() =>
            {
                if (generation == Generation && _wanted != IntPtr.Zero) Fail(L10n.Text("rekordbox window not found"));
            });
            var size = item.Size;
            var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_direct3D.Device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);
            var session = pool.CreateCaptureSession(item);
            lock (_frameGate)
            {
                _item = item;
                _pool = pool;
                _session = session;
                _poolSize = size;
                _deliveringGeneration = generation;
            }
            pool.FrameArrived += OnFrameArrived;
            session.IsCursorCaptureEnabled = false;
            HideBorder(session);
            session.StartCapture();
            SetStatus(new CaptureStatus(CaptureStatusKind.Running));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            TearDown();
            Fail(error.Message);
        }
    }

    /// Windows 11 draws a yellow border around a captured window unless the app may capture without it.
    private static void HideBorder(GraphicsCaptureSession session)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348)) return;
        try
        {
            if (!_askedForBorderless)
            {
                _askedForBorderless = true;
                _ = GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless);
            }
            session.IsBorderRequired = false;
        }
        catch (Exception error) when (error is UnauthorizedAccessException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            // This Windows keeps the border; the capture works with it.
        }
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        RawFrame raw;
        try
        {
            lock (_frameGate)
            {
                if (!ReferenceEquals(sender, _pool) || _direct3D is not { } direct3D) return;
                using var frame = sender.TryGetNextFrame();
                if (frame is null) return;
                var content = frame.ContentSize;
                var width = Math.Min(content.Width, _poolSize.Width);
                var height = Math.Min(content.Height, _poolSize.Height);
                if (width <= 0 || height <= 0) return;
                NativeMethods.ExtendedFrameBounds(_wanted, out var bounds);
                NativeMethods.GetWindowRect(_wanted, out var windowRect);
                var stride = width * 4;
                var bytes = _pixels.Rent(stride * height);
                direct3D.Copy(frame.Surface, width, height, bytes, stride);
                raw = new RawFrame(new FramePixels(_pixels, bytes, width, height, stride), frame.SystemRelativeTime.TotalSeconds, bounds, windowRect,
                    _deliveringGeneration);
                if (content.Width != _poolSize.Width || content.Height != _poolSize.Height)
                {
                    _poolSize = content;
                    sender.Recreate(direct3D.Device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, content);
                }
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            var message = error.Message;
            var generation = _deliveringGeneration;
            _dispatcher.BeginInvoke(() =>
            {
                if (generation == Generation && Status.IsRunning)
                {
                    TearDown();
                    Fail(message);
                }
            });
            return;
        }
        if (FrameArrived is { } deliver) deliver(raw);
        else raw.Pixels.Release();
    }

    private void TearDown()
    {
        _retryTimer?.Stop();
        Direct3D11CaptureFramePool? pool;
        GraphicsCaptureSession? session;
        lock (_frameGate)
        {
            pool = _pool;
            session = _session;
            _pool = null;
            _session = null;
            _item = null;
            _deliveringGeneration = 0;
        }
        if (pool is not null) pool.FrameArrived -= OnFrameArrived;
        session?.Dispose();
        pool?.Dispose();
    }

    private void Fail(string reason) => Retry(RetrySchedule.Reason.Failure, new CaptureStatus(CaptureStatusKind.Failed, reason));

    private void Retry(RetrySchedule.Reason reason, CaptureStatus status)
    {
        if (_schedule.Retry(reason, CaptureClock.Now) is { } timer)
        {
            _retryTimer?.Stop();
            var retry = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher) { Interval = TimeSpan.FromSeconds(timer.Delay) };
            retry.Tick += (_, _) =>
            {
                retry.Stop();
                if (!_schedule.Fire(timer)) return;
                if (_wanted == IntPtr.Zero || Status.IsRunning || Status.Kind == CaptureStatusKind.Starting) return;
                Start();
            };
            _retryTimer = retry;
            retry.Start();
        }
        SetStatus(status);
    }

    private void SetStatus(CaptureStatus status)
    {
        if (status == Status) return;
        Status = status;
        StatusChanged?.Invoke(status);
    }

    public void Dispose()
    {
        Stop();
        lock (_frameGate)
        {
            _direct3D?.Dispose();
            _direct3D = null;
        }
    }
}
