using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Earmatch.App.Controls;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Drawing;

public sealed class PhaseMeterCanvas : FrameworkElement
{
    private const double RowPitch = 18;
    private const double SideColumn = 76;
    private const double LabelColumn = 64;
    private const double BadgeColumn = 26;
    private static readonly Brush MasterBand = WindowTheme.Solid(Color.FromRgb(15, 15, 15));
    private static readonly Brush MasterWave = WindowTheme.Solid(WindowTheme.CdjBlueColor, 0.85);

    private PhaseMeterDisplay? _display;

    public PhaseMeterDisplay? Display
    {
        get => _display;
        set
        {
            if (Equals(_display, value)) return;
            _display = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext context)
    {
        if (_display is not { Rows.Count: > 0 } display) return;
        var size = new Size(ActualWidth, ActualHeight);
        var style = display.Style;
        var rows = display.Rows;
        var midY = size.Height / 2;
        var firstY = midY - (rows.Count - 1) * RowPitch / 2;
        var meter = display.CounterSide == PhaseMeterCounterSide.Right
            ? new Rect(LabelColumn + 6, 0, Math.Max(48, size.Width - LabelColumn - 6 - SideColumn - 10), size.Height)
            : new Rect(SideColumn + 10, 0, Math.Max(48, size.Width - SideColumn - 10 - BadgeColumn), size.Height);

        if (style == PhaseMeterType.Cdj3000Waveform) DrawMasterBand(context, display, meter, firstY);
        DrawLabel(context, display, meter, midY, firstY);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var y = firstY + index * RowPitch;
            var dimmed = row.BeatInBar is null && !row.IsVacant;
            if (dimmed) context.PushOpacity(0.35);
            switch (style)
            {
                case PhaseMeterType.Box: DrawBoxes(context, display, row, meter, y); break;
                case PhaseMeterType.Line or PhaseMeterType.Cdj3000PhaseMeter: DrawRuler(context, display, row, meter, y); break;
                default:
                    if (row.Role == PhaseMeterRole.Deck) DrawRuler(context, display, row, meter, y);
                    break;
            }
            DrawCounter(context, display, row, meter, y);
            if (dimmed) context.Pop();
        }

        if (style != PhaseMeterType.Box)
        {
            var lastY = firstY + (rows.Count - 1) * RowPitch;
            var top = style == PhaseMeterType.Cdj3000Waveform ? firstY - 9 : firstY - 10;
            context.DrawRectangle(WindowTheme.WhiteBrush, null, new Rect(meter.X + meter.Width / 2 - 1, top, 2, lastY - top + 10));
        }
    }

    private static Brush ColorOf(PhaseMeterDisplay display, PhaseMeterRow row) => (row.Role, display.Style.IsCDJ3000) switch
    {
        (PhaseMeterRole.Master, false) => WindowTheme.Master,
        (PhaseMeterRole.Deck, false) => WindowTheme.Deck,
        (PhaseMeterRole.Master, true) => WindowTheme.CdjOrange,
        _ => WindowTheme.CdjBlue,
    };

    private double Dpi => VisualTreeHelper.GetDpi(this).PixelsPerDip;

    private void DrawLabel(DrawingContext context, PhaseMeterDisplay display, Rect meter, double midY, double firstY)
    {
        if (display.Label is not { } label) return;
        const double size = 15;
        var isRight = display.CounterSide == PhaseMeterCounterSide.Left;
        var badge = isRight
            ? new Rect(meter.Right + 6, firstY - size / 2, size, size)
            : new Rect(meter.Left - 8 - size, midY - size / 2, size, size);
        var orange = display.Style.IsCDJ3000 ? WindowTheme.CdjOrange : WindowTheme.Master;
        context.DrawRoundedRectangle(orange, null, badge, 2, 2);
        var number = Ui.Formatted(label.Player.ToString(CultureInfo.InvariantCulture), AppFonts.Text, 11.5, FontWeights.Black, WindowTheme.Black, Dpi);
        Ui.DrawText(context, number, badge.X + badge.Width / 2, badge.Y + badge.Height / 2, TextAnchor.Center);
        if (isRight) return;
        for (var index = 0; index < label.Lines.Count; index++)
        {
            var y = midY + (index - (label.Lines.Count - 1) / 2.0) * 9;
            var line = Ui.Formatted(label.Lines[index], AppFonts.Text, 7.5, FontWeights.Black, orange, Dpi);
            Ui.DrawText(context, line, badge.Left - 4, y, TextAnchor.Trailing);
        }
    }

    private void DrawCounter(DrawingContext context, PhaseMeterDisplay display, PhaseMeterRow row, Rect meter, double y)
    {
        var brush = ColorOf(display, row);
        var text = Ui.Formatted(row.Counter + " " + PhaseMeterDisplay.CounterUnit, AppFonts.Text, TypeStyle.Counter.Size, TypeStyle.Counter.Weight, brush, Dpi);
        text.SetFontSize(8, row.Counter.Length, PhaseMeterDisplay.CounterUnit.Length + 1);
        text.SetFontWeight(FontWeights.SemiBold, row.Counter.Length, PhaseMeterDisplay.CounterUnit.Length + 1);
        if (display.CounterSide == PhaseMeterCounterSide.Right) Ui.DrawText(context, text, meter.Right + 10, y, TextAnchor.Leading);
        else Ui.DrawText(context, text, meter.Left - 10, y, TextAnchor.Trailing);
    }

    private static void DrawBoxes(DrawingContext context, PhaseMeterDisplay display, PhaseMeterRow row, Rect meter, double y)
    {
        const double gap = 6;
        const double height = 11;
        var width = (meter.Width - 3 * gap) / 4;
        var dim = row.Role == PhaseMeterRole.Master ? WindowTheme.MasterDim : WindowTheme.DeckDim;
        for (var index = 0; index < row.Boxes.Count; index++)
        {
            var rect = new Rect(meter.Left + index * (width + gap), y - height / 2, width, height);
            if (index < 3) context.DrawRectangle(dim, null, new Rect(rect.Right, y - 0.75, gap, 1.5));
            switch (row.Boxes[index])
            {
                case PhaseMeterBox.Idle:
                    context.DrawRectangle(null, new Pen(dim, 1), Inset(rect, 0.5));
                    break;
                case PhaseMeterBox.MasterBeat:
                    context.DrawRectangle(null, new Pen(ColorOf(display, row), 2), Inset(rect, 1));
                    break;
                case PhaseMeterBox.InPhase:
                    context.DrawRectangle(WindowTheme.Deck, null, rect);
                    break;
                default:
                    context.DrawRectangle(WindowTheme.Downbeat, null, rect);
                    break;
            }
        }
    }

    private static Rect Inset(Rect rect, double amount) => new(rect.X + amount, rect.Y + amount, rect.Width - 2 * amount, rect.Height - 2 * amount);

    private static void DrawRuler(DrawingContext context, PhaseMeterDisplay display, PhaseMeterRow row, Rect meter, double y)
    {
        var isCdj = display.Style.IsCDJ3000;
        var thickness = isCdj ? 2 : 1.5;
        context.DrawRectangle(ColorOf(display, row), null, new Rect(meter.Left, y - thickness / 2, meter.Width, thickness));
        var length = isCdj ? 7.0 : 5.0;
        var toward = row.TickDirection == PhaseMeterTickDirection.Up ? -1.0 : 1.0;
        var midX = meter.X + meter.Width / 2;
        var beatBrush = isCdj ? WindowTheme.WhiteBrush : WindowTheme.TickGrey;
        foreach (var tick in row.Ticks)
        {
            var x = midX + tick.Position * meter.Width / 2;
            if (tick.IsDownbeat)
            {
                double near = y + toward * length * 1.15, far = y - toward * length * 0.9;
                context.DrawRectangle(WindowTheme.Downbeat, null, new Rect(x - 1, Math.Min(near, far), 2, Math.Abs(near - far)));
            }
            else
            {
                context.DrawRectangle(beatBrush, null, new Rect(x - 0.7, Math.Min(y, y + toward * length), 1.4, length));
            }
        }
    }

    private static void DrawMasterBand(DrawingContext context, PhaseMeterDisplay display, Rect meter, double firstY)
    {
        var band = new Rect(meter.Left, firstY - 8, meter.Width, 16);
        context.DrawRectangle(MasterBand, null, band);
        if (display.Rows.Count == 0 || display.Rows[0] is not { Role: PhaseMeterRole.Master, IsVacant: false } master) return;
        var pointsPerBeat = meter.Width / 2 / display.Span;
        var midX = meter.X + meter.Width / 2;
        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            foreach (var tick in master.Ticks)
            {
                var start = midX + tick.Position * meter.Width / 2;
                for (var step = 0; step < 8; step++)
                {
                    var x = start + step * pointsPerBeat / 10;
                    if (x < band.Left || x > band.Right) continue;
                    var height = band.Height * (tick.IsDownbeat ? 0.95 : 0.8) * Math.Pow(0.72, step);
                    AddRect(stream, new Rect(x, band.Y + band.Height / 2 - height / 2, 1.2, height));
                }
            }
        }
        geometry.Freeze();
        context.DrawGeometry(MasterWave, null, geometry);
    }

    internal static void AddRect(StreamGeometryContext stream, Rect rect)
    {
        stream.BeginFigure(rect.TopLeft, isFilled: true, isClosed: true);
        stream.PolyLineTo([rect.TopRight, rect.BottomRight, rect.BottomLeft], isStroked: false, isSmoothJoin: false);
    }
}
