using System.Windows.Media;
using Earmatch.App.Capture;

namespace Earmatch.App.Overlay;

/// Calls back once per frame WPF draws, at the display's refresh rate (DisplayLinkDriver.swift's CADisplayLink), with
/// the screen times of that frame on CaptureClock: `timestamp`, the next vertical blank, the earliest a change made now
/// can be seen, and `targetTime`, the blank after it, when WPF's render thread and the desktop composition have shown
/// it (the time the meters are predicted for). The times come from the desktop composition's timing, or 60 Hz without.
internal sealed class RefreshDriver
{
    private const double FallbackPeriod = 1.0 / 60;
    private bool _isRunning;

    public Action<double, double>? Refresh { get; set; }

    public bool IsRunning => _isRunning;

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var (timestamp, target) = ScreenTimes(CaptureClock.Now);
        Refresh?.Invoke(timestamp, target);
    }

    public static (double Timestamp, double Target) ScreenTimes(double now)
    {
        double vblank, period;
        if (NativeMethods.CompositionTiming(out var vblankTicks, out var periodTicks))
        {
            vblank = CaptureClock.Seconds(vblankTicks);
            period = CaptureClock.Seconds(periodTicks);
        }
        else
        {
            vblank = now;
            period = FallbackPeriod;
        }
        var next = vblank + Math.Max(1, Math.Ceiling((now - vblank) / period)) * period;
        return (next, next + period);
    }
}
