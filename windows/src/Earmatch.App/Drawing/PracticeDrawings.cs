using System.Windows;
using System.Windows.Media;
using Earmatch.App.Controls;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Drawing;

/// The phase error meter of the practice pane (PracticeErrorMeter.swift): behind on the left, ahead on the right, the
/// in-phase band in the middle.
public sealed class PracticeErrorMeter : FrameworkElement
{
    public const double Range = 50;
    public const double InPhase = 10;
    private static readonly Brush Band = WindowTheme.Solid(WindowTheme.DeckColor, 0.14);

    private double? _error;
    private bool _isDimmed;

    public static Brush BrushFor(double errorMilliseconds) => Math.Abs(errorMilliseconds) <= InPhase ? WindowTheme.Deck : WindowTheme.Downbeat;

    public void Show(double? errorMilliseconds, bool isDimmed)
    {
        _error = errorMilliseconds;
        _isDimmed = isDimmed;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        double width = ActualWidth, height = ActualHeight;
        context.DrawRoundedRectangle(WindowTheme.Black, null, new Rect(0, 0, width, height), 6, 6);
        const double inset = 14;
        var track = new Rect(inset, 0, Math.Max(0, width - 2 * inset), height);
        var midY = height / 2 + 4;
        var midX = track.X + track.Width / 2;
        double X(double milliseconds) => midX + Math.Clamp(milliseconds, -Range, Range) / Range * track.Width / 2;

        context.DrawRoundedRectangle(Band, null, new Rect(X(-InPhase), midY - 8, X(InPhase) - X(-InPhase), 16), 3, 3);
        context.DrawRectangle(WindowTheme.DeckDim, null, new Rect(track.X, midY - 0.75, track.Width, 1.5));
        for (var milliseconds = -Range; milliseconds <= Range; milliseconds += 12.5)
        {
            if (milliseconds == 0) continue;
            var tickHeight = milliseconds % 25 == 0 ? 8 : 4;
            context.DrawRectangle(WindowTheme.DeckDim, null, new Rect(X(milliseconds) - 0.6, midY - tickHeight / 2.0, 1.2, tickHeight));
        }
        context.DrawRectangle(WindowTheme.WhiteBrush, null, new Rect(midX - 1.25, midY - 11, 2.5, 22));

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        Ui.DrawText(context, Ui.Formatted(PracticeStrings.Behind, AppFonts.Text, 7.5, FontWeights.Black, WindowTheme.SectionText, dpi), track.Left, 7, TextAnchor.Leading);
        Ui.DrawText(context, Ui.Formatted(PracticeStrings.Ahead, AppFonts.Text, 7.5, FontWeights.Black, WindowTheme.SectionText, dpi), track.Right, 7, TextAnchor.Trailing);

        if (_error is not { } error) return;
        var marker = new Rect(X(error) - 7, midY - 6, 14, 12);
        var brush = BrushFor(error);
        if (_isDimmed) context.PushOpacity(0.45);
        context.DrawRoundedRectangle(brush, null, marker, 2, 2);
        if (_isDimmed) context.Pop();
        if (Math.Abs(error) <= Range) return;
        var edge = error > 0 ? marker.Right + 3 : marker.Left - 3;
        var direction = error > 0 ? 1 : -1;
        var arrow = new StreamGeometry();
        using (var stream = arrow.Open())
        {
            stream.BeginFigure(new Point(edge, midY - 4), true, true);
            stream.PolyLineTo([new Point(edge + 4 * direction, midY), new Point(edge, midY + 4)], false, false);
        }
        context.DrawGeometry(brush, null, arrow);
    }
}

/// The replay's trace of the phase error over the last transition (PracticeTraceDrawing in PracticeReplayView.swift).
public sealed class PracticeTraceView : FrameworkElement
{
    private static readonly Brush BandFill = WindowTheme.Solid(WindowTheme.DeckColor, 0.12);
    private static readonly Brush ZeroLine = WindowTheme.White(0.28);
    private static readonly Brush BeforeMatch = WindowTheme.Solid(Colors.Black, 0.35);
    private static readonly Pen MatchedLine = new(WindowTheme.Solid(WindowTheme.AccentColor, 0.7), 1) { DashStyle = new DashStyle([2, 2], 0) };
    private static readonly Pen BeforePen = new(WindowTheme.White(0.58 * 0.6), 1);
    private static readonly Pen AfterPen = new(WindowTheme.Deck, 1.5) { LineJoin = PenLineJoin.Round };
    private static readonly Brush CursorLine = WindowTheme.White(0.85);

    private PracticeTrace? _trace;
    private double? _cursor;

    public void Show(PracticeTrace trace, double? cursor)
    {
        _trace = trace;
        _cursor = cursor;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        if (_trace is not { } trace) return;
        double width = ActualWidth, height = ActualHeight;
        if (width <= 0 || height <= 0) return;
        var duration = Math.Max(trace.DurationSeconds, 0.1);
        var matched = trace.MatchedAtSeconds;
        var worst = trace.WorstSample is { } sample ? Math.Abs(sample.ErrorMilliseconds) : 0;
        var scale = matched is null ? 100 : Math.Min(Math.Max(worst * 1.25, PracticeErrorMeter.InPhase * 2), 100);
        var midY = height / 2;
        // The trace keeps 2 points from the edges; a trace shorter than that draws flat instead of upside down.
        var reach = Math.Max(0, height / 2 - 2);
        double X(double seconds) => seconds / duration * width;
        double Y(double milliseconds) => midY - Math.Clamp(milliseconds, -scale, scale) / scale * reach;

        var bandTop = Y(PracticeErrorMeter.InPhase);
        context.DrawRectangle(BandFill, null, new Rect(0, bandTop, width, Y(-PracticeErrorMeter.InPhase) - bandTop));
        context.DrawRectangle(ZeroLine, null, new Rect(0, midY - 0.5, width, 1));
        if (matched is { } matchedAt)
        {
            var line = X(matchedAt);
            context.DrawRectangle(BeforeMatch, null, new Rect(0, 0, Math.Max(0, line), height));
            context.DrawLine(MatchedLine, new Point(line, 0), new Point(line, height));
        }
        context.DrawGeometry(null, BeforePen, Path(trace, matched ?? double.PositiveInfinity, X, Y, width, matchedSide: false));
        context.DrawGeometry(null, AfterPen, Path(trace, matched ?? double.PositiveInfinity, X, Y, width, matchedSide: true));

        if (trace.WorstSample is { } worstSample && _cursor != worstSample.Seconds)
        {
            context.DrawEllipse(WindowTheme.Downbeat, null, new Point(X(worstSample.Seconds), Y(worstSample.ErrorMilliseconds)), 2.5, 2.5);
        }
        if (_cursor is not { } cursor || trace.ErrorMilliseconds(cursor) is not { } error) return;
        var cursorX = X(cursor);
        context.DrawRectangle(CursorLine, null, new Rect(cursorX - 0.75, 0, 1.5, height));
        context.DrawEllipse(PracticeErrorMeter.BrushFor(error), new Pen(WindowTheme.WhiteBrush, 1.2), new Point(cursorX, Y(error)), 4, 4);
    }

    private static StreamGeometry Path(PracticeTrace trace, double matched, Func<double, double> x, Func<double, double> y, double width, bool matchedSide)
    {
        var samples = trace.Samples.Where(sample => sample.Seconds >= matched == matchedSide).ToList();
        var bucketWidth = Math.Max(1, (int)Math.Ceiling(samples.Count / Math.Max(width, 1)));
        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            double? previous = null;
            for (var index = 0; index < samples.Count; index += bucketWidth)
            {
                var bucket = samples.Skip(index).Take(bucketWidth).ToList();
                var low = bucket.MinBy(sample => sample.ErrorMilliseconds);
                var high = bucket.MaxBy(sample => sample.ErrorMilliseconds);
                var ordered = low == high ? [low] : low.Seconds <= high.Seconds ? new[] { low, high } : [high, low];
                foreach (var sample in ordered)
                {
                    var point = new Point(x(sample.Seconds), y(sample.ErrorMilliseconds));
                    if (previous is { } last && sample.Seconds - last <= 0.5) stream.LineTo(point, true, true);
                    else stream.BeginFigure(point, false, false);
                    previous = sample.Seconds;
                }
            }
        }
        geometry.Freeze();
        return geometry;
    }
}

/// The progress chart: the median phase error of each day, newest on the right (PracticeProgressChart.swift).
public sealed class PracticeProgressView : FrameworkElement
{
    private static readonly Pen Threshold = new(WindowTheme.Solid(WindowTheme.DeckColor, 0.45), 1) { DashStyle = new DashStyle([3, 3], 0) };
    private static readonly Brush DeckFaded = WindowTheme.Solid(WindowTheme.DeckColor, 0.55);

    private IReadOnlyList<double?> _values = [];

    public IReadOnlyList<double?> Values
    {
        set
        {
            _values = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext context)
    {
        if (_values.Count == 0) return;
        double width = ActualWidth, height = ActualHeight;
        var top = Math.Max(_values.OfType<double>().DefaultIfEmpty(0).Max(), PracticeErrorMeter.InPhase * 2) * 1.1;
        var slots = Math.Max(_values.Count, 7);
        var slot = width / slots;
        var barWidth = Math.Min(slot * 0.62, 18);
        var origin = width - _values.Count * slot;
        double Y(double milliseconds) => height - milliseconds / top * height;

        var threshold = Y(PracticeErrorMeter.InPhase);
        context.DrawLine(Threshold, new Point(0, threshold), new Point(width, threshold));
        context.DrawRectangle(WindowTheme.Hairline, null, new Rect(0, height - 1, width, 1));
        for (var index = 0; index < _values.Count; index++)
        {
            var centre = origin + (index + 0.5) * slot;
            if (_values[index] is not { } value)
            {
                context.DrawEllipse(WindowTheme.SectionText, null, new Point(centre, height - 4), 2, 2);
                continue;
            }
            var barHeight = Math.Max(height - Y(value), 3);
            var isLatest = index == _values.Count - 1;
            var brush = value <= PracticeErrorMeter.InPhase
                ? isLatest ? WindowTheme.Deck : DeckFaded
                : isLatest ? WindowTheme.Master : WindowTheme.MasterDim;
            context.DrawRoundedRectangle(brush, null, new Rect(centre - barWidth / 2, height - barHeight, barWidth, barHeight), 3, 3);
        }
    }
}
