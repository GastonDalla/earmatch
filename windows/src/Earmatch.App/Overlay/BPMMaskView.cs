using System.Windows.Controls;
using System.Windows.Shapes;
using Earmatch.Core;

namespace Earmatch.App.Overlay;

internal sealed class BPMMaskView : Canvas
{
    public const double CoverMargin = 3;

    private readonly Rectangle _cover = new() { Fill = OverlayColors.Black };
    private ScreenRect _shown;

    public BPMMaskView()
    {
        Children.Add(_cover);
    }

    public void Show(ScreenRect cover, ScreenPoint overlayOrigin, double scale)
    {
        var local = new ScreenRect((cover.X - overlayOrigin.X) / scale, (cover.Y - overlayOrigin.Y) / scale, cover.Width / scale, cover.Height / scale);
        if (local == _shown) return;
        _shown = local;
        SetLeft(_cover, local.X);
        SetTop(_cover, local.Y);
        _cover.Width = Math.Max(0, local.Width);
        _cover.Height = Math.Max(0, local.Height);
    }
}
