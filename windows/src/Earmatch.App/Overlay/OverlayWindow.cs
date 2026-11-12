using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Earmatch.Core;

namespace Earmatch.App.Overlay;

/// One overlay over rekordbox (OverlayPanel.swift): a borderless, always-on-top popup that lets every click through to
/// rekordbox (WS_EX_TRANSPARENT), never takes the focus (WS_EX_NOACTIVATE), stays out of Alt+Tab and the taskbar
/// (WS_EX_TOOLWINDOW) and is drawn with per-pixel alpha (WS_EX_LAYERED). It is placed in physical screen pixels, the
/// coordinates of rekordbox's UI Automation frames; WPF scales its content to the monitor's DPI (the app is per-monitor
/// DPI aware). It hosts any WPF visual: a solid cover, a BPM mask, a phase meter or the split waveform. An opaque
/// overlay (the split waveform panel) skips the per-pixel alpha: it stays layered for the click-through, at full opacity.
internal sealed class OverlayWindow : IDisposable
{
    private readonly HwndSource _source;
    private Int32Rect _placed = Int32Rect.Empty;
    private bool _isShown;

    public OverlayWindow(Visual content, bool isOpaque = false)
    {
        var parameters = new HwndSourceParameters("Earmatch overlay")
        {
            WindowStyle = NativeMethods.WsPopup,
            ExtendedWindowStyle = NativeMethods.WsExLayered | NativeMethods.WsExTransparent | NativeMethods.WsExNoActivate
                | NativeMethods.WsExToolWindow | NativeMethods.WsExTopmost,
            UsesPerPixelTransparency = !isOpaque,
            PositionX = 0,
            PositionY = 0,
            Width = 1,
            Height = 1,
        };
        _source = new HwndSource(parameters) { RootVisual = content };
        if (isOpaque) NativeMethods.SetLayeredWindowAttributes(_source.Handle, 0, 255, NativeMethods.LwaAlpha);
        // On a monitor with another scale WPF resizes the window to its suggested rectangle; the frame stays the one
        // rekordbox's layout asks for.
        _source.DpiChanged += (_, _) => _source.Dispatcher.BeginInvoke(Reapply);
    }

    /// A solid cover of one colour.
    public OverlayWindow(Color color) : this(new Border { Background = new SolidColorBrush(color) })
    {
    }

    public IntPtr Handle => _source.Handle;

    public bool IsShown => _isShown;

    /// The physical screen pixel of the overlay's top-left corner, as last placed.
    public ScreenPoint PlacedOrigin => new(_placed.X, _placed.Y);

    /// Shows the overlay over `frame` moved by `offset`, both in physical screen pixels, rounded outwards to whole
    /// pixels so a cover never leaves a sliver of what it covers.
    public void Place(ScreenRect frame, ScreenPoint offset)
    {
        var left = (int)Math.Floor(frame.MinX + offset.X);
        var top = (int)Math.Floor(frame.MinY + offset.Y);
        var rect = new Int32Rect(left, top, Math.Max(1, (int)Math.Ceiling(frame.MaxX + offset.X) - left),
            Math.Max(1, (int)Math.Ceiling(frame.MaxY + offset.Y) - top));
        if (_isShown && rect == _placed) return;
        _placed = rect;
        _isShown = true;
        Reapply();
    }

    public void Hide()
    {
        if (!_isShown) return;
        _isShown = false;
        NativeMethods.ShowWindow(Handle, NativeMethods.SwHide);
    }

    private void Reapply()
    {
        if (!_isShown || _source.IsDisposed) return;
        NativeMethods.SetWindowPos(Handle, NativeMethods.HwndTopmost, _placed.X, _placed.Y, _placed.Width, _placed.Height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow | NativeMethods.SwpNoOwnerZOrder);
    }

    public void Dispose() => _source.Dispose();
}
