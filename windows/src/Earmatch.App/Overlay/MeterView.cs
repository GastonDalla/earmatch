using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Overlay;

internal static class OverlayColors
{
    public static readonly Brush MasterOrange = Frozen(240, 140, 40);
    public static readonly Brush MasterOrangeDim = Frozen(130, 72, 22);
    public static readonly Brush DeckBlue = Frozen(60, 140, 220);
    public static readonly Brush DeckBlueDim = Frozen(30, 68, 110);
    public static readonly Brush DownbeatRed = Frozen(230, 50, 40);
    public static readonly Brush NowWhite = Frozen(255, 255, 255);
    public static readonly Brush Black = Frozen(0, 0, 0);
    public static readonly Brush Divider = Frozen(52, 52, 56);
    public static readonly Brush Band = Frozen(0, 0, 0, 0.82);
    public static readonly Brush CdjOrange = Frozen(255, 115, 0);
    public static readonly Brush CdjBlue = Frozen(0, 125, 255);
    public static readonly Brush TickGrey = Frozen(205, 205, 205);
    public static readonly Brush BandEmpty = Frozen(14, 14, 16);
    public static readonly Color HeaderGrey = Color.FromRgb(30, 30, 30);

    public static Brush Row(bool isMaster, bool isCdj3000) => (isMaster, isCdj3000) switch
    {
        (true, false) => MasterOrange,
        (false, false) => DeckBlue,
        (true, true) => CdjOrange,
        _ => CdjBlue,
    };

    private static Brush Frozen(byte red, byte green, byte blue, double alpha = 1)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(alpha * 255), red, green, blue));
        brush.Freeze();
        return brush;
    }
}

internal sealed class MeterView : FrameworkElement
{
    public const double CompactWidth = 380;
    public const double CompactHeight = 34;

    private static readonly Typeface CounterFace = new(AppFonts.Text, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
    private static readonly Typeface UnitFace = new(AppFonts.Text, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface LabelFace = new(AppFonts.Text, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly Typeface BadgeFace = new(AppFonts.Text, FontStyles.Normal, FontWeights.Black, FontStretches.Normal);

    private PhaseMeterDisplay? _display;
    private ImageSource? _bandImage;

    public double RowsCenter { get; init; } = 0.45;

    public double PlayheadExtension { get; set; }

    public bool AllowsMasterWaveform { get; set; }

    public Rect? MeterBounds { get; set; }

    public Brush? Plate { get; init; }

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

    public ImageSource? BandImage
    {
        get => _bandImage;
        set
        {
            if (ReferenceEquals(_bandImage, value)) return;
            _bandImage = value;
            InvalidateVisual();
        }
    }

    public Rect? BandRect => _display is { Rows.Count: > 0 } display && UsesBand(display)
        ? Geometry.For(display.Style, Bounds, RowsCenter, Math.Min(display.Rows.Count, 2), usesBand: true).Band
        : null;

    public bool IsWaitingForMasterWaveform => BandRect is not null && _display?.ShowsMasterWaveform == true && _bandImage is null;

    private Rect Bounds => MeterBounds ?? new Rect(0, 0, ActualWidth, ActualHeight);

    private bool UsesBand(PhaseMeterDisplay display) => display.Style == PhaseMeterType.Cdj3000Waveform && AllowsMasterWaveform;

    public void Refresh() => InvalidateVisual();

    protected override void OnRender(DrawingContext context)
    {
        if (_display is not { Rows.Count: > 0 } display) return;
        if (Plate is { } plate) context.DrawRoundedRectangle(plate, null, new Rect(0, 0, ActualWidth, ActualHeight), 3, 3);
        var bounds = Bounds;
        var style = display.Style;
        var rowCount = Math.Min(display.Rows.Count, 2);
        var usesBand = UsesBand(display);
        var geometry = Geometry.For(style, bounds, RowsCenter, rowCount, usesBand);
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        if (geometry.Band is { } band)
        {
            context.DrawRectangle(OverlayColors.BandEmpty, null, band);
            if (display.ShowsMasterWaveform && _bandImage is { } image) context.DrawImage(image, band);
        }
        for (var index = 0; index < rowCount; index++) DrawRow(context, display.Rows[index], geometry, geometry.RowY(index), dip);
        if (display.Label is { } label) DrawLabel(context, label, style, geometry, dip);

        if (style != PhaseMeterType.Box)
        {
            double top, bottom;
            if (geometry.Band is { } shown)
            {
                top = shown.Top - 3;
                bottom = bounds.Bottom + PlayheadExtension;
            }
            else
            {
                top = geometry.RowY(0) - geometry.TickLength * 1.4;
                bottom = geometry.RowY(rowCount - 1) + geometry.TickLength * 1.4;
            }
            var width = style.IsCDJ3000 ? 2 : 1.5;
            context.DrawRectangle(OverlayColors.NowWhite, null, new Rect(geometry.CenterX - width / 2, top, width, Math.Max(0, bottom - top)));
        }
    }

    private static void DrawRow(DrawingContext context, PhaseMeterRow row, Geometry geometry, double y, double dip)
    {
        var style = geometry.Style;
        var isMaster = row.Role == PhaseMeterRole.Master;
        var colour = OverlayColors.Row(isMaster, style.IsCDJ3000);
        var dimmed = row.BeatInBar is null && !row.IsVacant;
        if (dimmed) context.PushOpacity(0.35);

        if (style == PhaseMeterType.Box)
        {
            const double gap = 6, boxHeight = 11;
            var boxWidth = (geometry.MeterWidth - 3 * gap) / 4;
            var dim = isMaster ? OverlayColors.MasterOrangeDim : OverlayColors.DeckBlueDim;
            for (var index = 0; index < 4; index++)
            {
                var box = new Rect(geometry.MeterLeft + index * (boxWidth + gap), y - boxHeight / 2, boxWidth, boxHeight);
                if (index < 3) context.DrawRectangle(dim, null, new Rect(box.Right, y - 0.75, gap, 1.5));
                var state = index < row.Boxes.Count ? row.Boxes[index] : PhaseMeterBox.Idle;
                switch (state)
                {
                    case PhaseMeterBox.Idle:
                        context.DrawRectangle(null, new Pen(dim, 1), Inset(box, 0.5));
                        break;
                    case PhaseMeterBox.MasterBeat:
                        context.DrawRectangle(null, new Pen(colour, 2), Inset(box, 1));
                        break;
                    case PhaseMeterBox.InPhase:
                        context.DrawRectangle(OverlayColors.DeckBlue, null, box);
                        break;
                    default:
                        context.DrawRectangle(OverlayColors.DownbeatRed, null, box);
                        break;
                }
            }
        }
        else if (geometry.Band is null)
        {
            var thickness = style.IsCDJ3000 ? 2 : 1.5;
            context.DrawRectangle(colour, null, new Rect(geometry.MeterLeft, y - thickness / 2, geometry.MeterWidth, thickness));
            var beatColour = style.IsCDJ3000 ? OverlayColors.NowWhite : OverlayColors.TickGrey;
            var toward = row.TickDirection == PhaseMeterTickDirection.Up ? -1.0 : 1.0;
            var length = geometry.TickLength;
            foreach (var tick in row.Ticks)
            {
                var x = geometry.CenterX + tick.Position * geometry.MeterWidth / 2;
                if (tick.IsDownbeat)
                {
                    double near = y + toward * length * 1.15, far = y - toward * length * 0.9;
                    context.DrawRectangle(OverlayColors.DownbeatRed, null, new Rect(x - 1, Math.Min(near, far), 2, Math.Abs(near - far)));
                }
                else
                {
                    context.DrawRectangle(beatColour, null, new Rect(x - 0.75, Math.Min(y, y + toward * length), 1.5, length));
                }
            }
        }

        var valueSize = style.IsCDJ3000 ? 13.5 : 12;
        const double unitSize = 7.5, valueWidth = 44, unitWidth = 24;
        var valueTop = y - 0.6 * valueSize;
        var unitTop = valueTop + 0.95 * (valueSize - unitSize);
        var edge = geometry.CounterEdge;
        var value = Text(row.Counter, CounterFace, valueSize, colour, dip);
        var unit = Text(PhaseMeterDisplay.CounterUnit, UnitFace, unitSize, colour, dip);
        if (style.IsCDJ3000)
        {
            context.DrawText(unit, new Point(edge - unitWidth, unitTop));
            context.DrawText(value, new Point(edge - unitWidth - 3 - value.Width, valueTop));
        }
        else
        {
            context.DrawText(value, new Point(edge + valueWidth - value.Width, valueTop));
            context.DrawText(unit, new Point(edge + valueWidth + 3, unitTop));
        }
        if (dimmed) context.Pop();
    }

    private static void DrawLabel(DrawingContext context, PhaseMeterLabel label, PhaseMeterType style, Geometry geometry, double dip)
    {
        const double size = 14;
        var badge = geometry.LabelIsLeft
            ? new Rect(geometry.LabelPoint.X - size, geometry.LabelPoint.Y - size / 2, size, size)
            : new Rect(geometry.LabelPoint.X, geometry.LabelPoint.Y - size / 2, size, size);
        context.DrawRoundedRectangle(style.IsCDJ3000 ? OverlayColors.CdjOrange : OverlayColors.MasterOrange, null, badge, 2, 2);
        var number = Text(label.Player.ToString(CultureInfo.InvariantCulture), BadgeFace, 11, OverlayColors.Black, dip);
        context.DrawText(number, new Point(badge.X + (size - number.Width) / 2, badge.Y + (size - number.Height) / 2));
        if (label.Lines.Count == 0) return;
        var right = badge.Left - 4;
        var offsets = new[] { -3.8, 3.8 };
        for (var index = 0; index < Math.Min(label.Lines.Count, 2); index++)
        {
            var line = Text(label.Lines[index], LabelFace, 6.5, OverlayColors.MasterOrange, dip);
            context.DrawText(line, new Point(right - line.Width, badge.Top + size / 2 + offsets[index] - 0.6 * 6.5));
        }
    }

    private static FormattedText Text(string text, Typeface face, double size, Brush brush, double dip) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, brush, dip);

    private static Rect Inset(Rect rect, double amount) =>
        new(rect.X + amount, rect.Y + amount, Math.Max(0, rect.Width - 2 * amount), Math.Max(0, rect.Height - 2 * amount));

    private readonly record struct Geometry(PhaseMeterType Style, double CenterX, double MeterWidth, double Pitch, double FirstRowY,
        double TickLength, Rect? Band, bool LabelIsLeft, Point LabelPoint)
    {
        public static Geometry For(PhaseMeterType style, Rect bounds, double rowsCenter, int rowCount, bool usesBand)
        {
            var centerX = bounds.X + bounds.Width / 2;
            double meterWidth, pitch;
            if (style is PhaseMeterType.Box or PhaseMeterType.Line)
            {
                meterWidth = 180;
                pitch = 15;
            }
            else
            {
                meterWidth = Math.Max(120, Math.Min(400, bounds.Width - 190));
                pitch = Math.Min(18, bounds.Height * 0.45);
            }
            var tickLength = style.IsCDJ3000 ? pitch * 0.45 : 5;
            if (usesBand)
            {
                var half = Math.Max(80, Math.Min(bounds.Width / 2 - 100, 320));
                var height = Math.Max(12, Math.Min(30, bounds.Height - 14));
                var band = new Rect(centerX - half, bounds.Bottom - 4 - height, 2 * half, height);
                var bandMid = band.Y + band.Height / 2;
                return new Geometry(style, centerX, meterWidth, pitch, bandMid - 8, tickLength, band, true, new Point(band.Left - 6, bandMid - 8));
            }
            var rowsMidY = bounds.Y + bounds.Height * rowsCenter;
            var firstRowY = rowsMidY - (rowCount - 1) * pitch / 2;
            return style is PhaseMeterType.Box or PhaseMeterType.Line
                ? new Geometry(style, centerX, meterWidth, pitch, firstRowY, tickLength, null, true, new Point(centerX - meterWidth / 2 - 8, rowsMidY))
                : new Geometry(style, centerX, meterWidth, pitch, firstRowY, tickLength, null, false, new Point(centerX + meterWidth / 2 + 8, firstRowY));
        }

        public double MeterLeft => CenterX - MeterWidth / 2;
        public double MeterRight => CenterX + MeterWidth / 2;

        public double RowY(int index) => Band is not null ? FirstRowY + index * 16 : FirstRowY + index * Pitch;

        public double CounterEdge => Band is { } band ? band.Left - 26 : Style.IsCDJ3000 ? MeterLeft - 10 : MeterRight + 10;
    }
}
