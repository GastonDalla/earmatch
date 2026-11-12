using System.Windows;
using System.Windows.Media;
using Earmatch.App.Theme;

namespace Earmatch.App.Controls;

public enum IconKind
{
    Live,
    Overlay,
    Metronome,
    Gear,
    Eye,
    EyeSlash,
    CheckCircleFill,
    ArrowDownCircle,
    Clipboard,
    Checkmark,
    PlayFill,
    StopFill,
    ArrowUpRight,
    ChartUptrend,
}

/// The SF Symbols the macOS window uses, drawn as vector paths on a 24-unit grid (or a Segoe Fluent Icons glyph where
/// Windows has a close match).
public sealed class Icon : FrameworkElement
{
    private Brush _brush;
    private IconKind _kind;

    public Icon(IconKind kind, double size, Brush brush, double weight = 2)
    {
        _kind = kind;
        IconSize = size;
        _brush = brush;
        Weight = weight;
        Width = size;
        Height = size;
        SnapsToDevicePixels = false;
    }

    public double IconSize { get; }
    public double Weight { get; }

    public IconKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            InvalidateVisual();
        }
    }

    public Brush Brush
    {
        get => _brush;
        set
        {
            _brush = value;
            InvalidateVisual();
        }
    }

    private static readonly Dictionary<IconKind, string> Glyphs = new()
    {
        [IconKind.Gear] = "",
        [IconKind.Eye] = "",
        [IconKind.EyeSlash] = "",
        [IconKind.Clipboard] = "",
    };

    protected override void OnRender(DrawingContext context)
    {
        if (Glyphs.TryGetValue(_kind, out var glyph))
        {
            var text = Ui.Formatted(glyph, AppFonts.Icons, IconSize * 0.92, FontWeights.Normal, _brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            context.DrawText(text, new Point((IconSize - text.Width) / 2, (IconSize - text.Height) / 2));
            return;
        }
        var scale = IconSize / 24;
        context.PushTransform(new ScaleTransform(scale, scale));
        // Weight is the stroke width on screen, in DIPs.
        var pen = RoundPen(_brush, Weight / scale);
        switch (_kind)
        {
            case IconKind.Live:
                context.DrawEllipse(_brush, null, new Point(12, 12), 2.3, 2.3);
                Stroke(context, pen, "M8.1,7.9 A5.6,5.6 0 0 0 8.1,16.1 M15.9,7.9 A5.6,5.6 0 0 1 15.9,16.1");
                Stroke(context, pen, "M5,4.8 A10,10 0 0 0 5,19.2 M19,4.8 A10,10 0 0 1 19,19.2");
                break;
            case IconKind.Overlay:
                Stroke(context, pen, "M3.5,14.5 V6 A2.5,2.5 0 0 1 6,3.5 H15 A2.5,2.5 0 0 1 17.5,6 V7.5");
                Stroke(context, pen, "M9,7.5 H18 A2.5,2.5 0 0 1 20.5,10 V18 A2.5,2.5 0 0 1 18,20.5 H9 A2.5,2.5 0 0 1 6.5,18 V10 A2.5,2.5 0 0 1 9,7.5 Z");
                break;
            case IconKind.Metronome:
                Stroke(context, pen, "M9.6,3.5 H14.4 L19.5,20.5 H4.5 Z");
                Stroke(context, pen, "M6.2,15 H17.8 M12,15 L17.5,6.5");
                break;
            case IconKind.CheckCircleFill:
                context.DrawEllipse(_brush, null, new Point(12, 12), 10.5, 10.5);
                Stroke(context, RoundPen(CheckInk, 2.6), "M7.2,12.4 L10.6,15.6 L16.8,8.6");
                break;
            case IconKind.ArrowDownCircle:
                context.DrawEllipse(null, pen, new Point(12, 12), 9.5, 9.5);
                Stroke(context, pen, "M12,7 V16.5 M8,12.8 L12,16.8 L16,12.8");
                break;
            case IconKind.Checkmark:
                Stroke(context, pen, "M5,12.5 L10,17.5 L19,6.5");
                break;
            case IconKind.PlayFill:
                Fill(context, "M6.5,3.8 Q6.5,2.6 7.6,3.2 L20.2,11 Q21.2,12 20.2,13 L7.6,20.8 Q6.5,21.4 6.5,20.2 Z");
                break;
            case IconKind.StopFill:
                context.DrawRoundedRectangle(_brush, null, new Rect(4.5, 4.5, 15, 15), 2.5, 2.5);
                break;
            case IconKind.ArrowUpRight:
                Stroke(context, pen, "M6,18 L18,6 M8.5,6 H18 V15.5");
                break;
            case IconKind.ChartUptrend:
                Stroke(context, pen, "M3.5,3.5 V20.5 H20.5 M6.5,16.5 L10.5,11.5 L14,14 L19.5,6.5");
                break;
        }
        context.Pop();
    }

    private static readonly Brush CheckInk = WindowTheme.Solid(Color.FromRgb(22, 22, 24), 0.92);

    private static Pen RoundPen(Brush brush, double thickness) =>
        new(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };

    private static void Stroke(DrawingContext context, Pen pen, string data) => context.DrawGeometry(null, pen, Geometry.Parse(data));

    private void Fill(DrawingContext context, string data) => context.DrawGeometry(_brush, null, Geometry.Parse(data));
}
