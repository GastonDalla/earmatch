using System.Windows.Controls;
using System.Windows.Shapes;
using Earmatch.Core;

namespace Earmatch.App.Overlay;

/// The cover over a jog's BPM (BPMMaskView.swift): a transparent overlay a little larger than the BPM text with a black
/// cover inside, over the digits the BPM limit hides (measured from the frame) or over all of them until measured.
internal sealed class BPMMaskView : Canvas
{
    /// How far the overlay reaches past the BPM text, in points.
    public const double CoverMargin = 3;

    private readonly Rectangle _cover = new() { Fill = OverlayColors.Black };
    private ScreenRect _shown;

    public BPMMaskView()
    {
        Children.Add(_cover);
    }

    /// Covers `cover`, both it and the overlay's origin in physical pixels; `scale` is the pixels per DIP.
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
