using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Earmatch.Core;

namespace Earmatch.App.Panes;

public sealed class MainWindowPlacement
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1);

    private readonly Window _window;
    private readonly SettingsStore _store;
    private readonly DispatcherTimer _saveTimer;
    private ScreenRect? _normalFrame;
    private bool _isRestored;

    public MainWindowPlacement(Window window, SettingsStore store)
    {
        _window = window;
        _store = store;
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher) { Interval = SaveDelay };
        _saveTimer.Tick += (_, _) => Save();
        window.LocationChanged += (_, _) => Track();
        window.SizeChanged += (_, _) => Track();
        window.StateChanged += (_, _) => Track();
        window.IsVisibleChanged += (_, _) =>
        {
            if (!window.IsVisible) Save();
        };
    }

    public void Restore()
    {
        if (_isRestored) return;
        _isRestored = true;
        var handle = new WindowInteropHelper(_window).EnsureHandle();
        var (monitors, primary) = NativeMethods.DisplayMonitors();
        if (primary is not { } main) return;
        var stored = WindowPlacement.FromStoredValue(_store.Value(WindowPlacement.StorageKey, WindowPlacement.SetupKey(monitors)));
        var placement = WindowPlacement.Restored(stored, monitors, main, MainWindowMetrics.DefaultSize, MainWindowMetrics.MinimumSize);
        var frame = placement.Frame;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            NativeMethods.SetWindowPos(handle, IntPtr.Zero, (int)frame.X, (int)frame.Y, (int)frame.Width, (int)frame.Height,
                NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
        }
        var scale = VisualTreeHelper.GetDpi(_window);
        _window.WindowStartupLocation = WindowStartupLocation.Manual;
        _window.Left = frame.X / scale.DpiScaleX;
        _window.Top = frame.Y / scale.DpiScaleY;
        _window.Width = frame.Width / scale.DpiScaleX;
        _window.Height = frame.Height / scale.DpiScaleY;
        _normalFrame = frame;
        if (placement.IsMaximized) _window.WindowState = WindowState.Maximized;
    }

    public void Save()
    {
        _saveTimer.Stop();
        if (!_isRestored || _normalFrame is not { } frame) return;
        var (monitors, _) = NativeMethods.DisplayMonitors();
        if (monitors.Count == 0) return;
        var placement = new WindowPlacement(frame, _window.WindowState == WindowState.Maximized);
        try
        {
            _store.SetValue(WindowPlacement.StorageKey, WindowPlacement.SetupKey(monitors), placement.StoredValue);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine($"earmatch: the window position was not saved: {exception.Message}");
        }
    }

    private void Track()
    {
        if (!_isRestored || !_window.IsVisible) return;
        if (_window.WindowState == WindowState.Normal && NormalFrame() is { } frame) _normalFrame = frame;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private ScreenRect? NormalFrame()
    {
        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero || NativeMethods.IsIconic(handle) || !NativeMethods.GetWindowRect(handle, out var rect)) return null;
        return rect.Width > 0 && rect.Height > 0 ? rect.Screen : null;
    }
}
