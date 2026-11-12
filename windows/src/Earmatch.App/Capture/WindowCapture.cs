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

internal readonly record struct RawFrame(FramePixels Pixels, double Time, NativeMethods.WindowRect Bounds, NativeMethods.WindowRect WindowRect,
    int Generation);

internal static class CaptureClock
{
    public static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    public static double Seconds(long ticks) => ticks / (double)Stopwatch.Frequency;
}

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

    public event Action<CaptureStatus>? StatusChanged;

    public Action<RawFrame>? FrameArrived { get; set; }

    public CaptureStatus Status { get; private set; } = CaptureStatus.Stopped;

    public int Generation { get; private set; }

    public IntPtr Window => _wanted;

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
            Direct3D direct3D;
            lock (_frameGate) direct3D = _direct3D ??= Direct3D.Create();
            var item = Direct3D.ItemForWindow(_wanted);
            item.Closed += (_, _) => _dispatcher.BeginInvoke(() =>
            {
                if (generation == Generation && _wanted != IntPtr.Zero) Fail(L10n.Text("rekordbox window not found"));
            });
            var size = item.Size;
            var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(direct3D.Device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);
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
            TearDown(dropDevice: true);
            Fail(error.Message);
        }
    }

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
                var copied = direct3D.Copy(frame.Surface, width, height, bytes, stride);
                if (copied.Width <= 0 || copied.Height <= 0)
                {
                    _pixels.Return(bytes);
                    return;
                }
                raw = new RawFrame(new FramePixels(_pixels, bytes, copied.Width, copied.Height, stride), frame.SystemRelativeTime.TotalSeconds, bounds,
                    windowRect, _deliveringGeneration);
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
                    TearDown(dropDevice: true);
                    Fail(message);
                }
            });
            return;
        }
        if (FrameArrived is { } deliver) deliver(raw);
        else raw.Pixels.Release();
    }

    private void TearDown(bool dropDevice = false)
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
        if (dropDevice) DropDevice();
    }

    private void DropDevice()
    {
        lock (_frameGate)
        {
            _direct3D?.Dispose();
            _direct3D = null;
        }
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
        DropDevice();
    }
}
