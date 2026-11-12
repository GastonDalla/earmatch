using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Earmatch.Core;

namespace Earmatch.App.Overlay;

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
        _source.DpiChanged += (_, _) => _source.Dispatcher.BeginInvoke(Reapply);
    }

    public OverlayWindow(Color color) : this(new Border { Background = new SolidColorBrush(color) })
    {
    }

    public IntPtr Handle => _source.Handle;

    public bool IsShown => _isShown;

    public ScreenPoint PlacedOrigin => new(_placed.X, _placed.Y);

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
