using System.Windows;
using System.Windows.Media;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Drawing;

/// The beats each deck's playhead is at, for the waveform preview.
public readonly record struct DeckPhases(double One, double Two);

/// The animated sample rekordbox session of the previews (PreviewClock.swift): the master at 128 BPM and deck 1 nudged
/// out of phase and back every six seconds.
public readonly struct PreviewClock
{
    public const double Bpm = 128;
    private const double Cycle = 12;
    private const double NudgeDuration = 0.45;
    private static readonly DateTimeOffset ReferenceDate = new(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public PreviewClock(DateTimeOffset date) => Seconds = (date - ReferenceDate).TotalSeconds % 60;

    public double Seconds { get; }

    public double MasterBeats => 20 + Seconds * Bpm / 60;

    public double DeckBeats => MasterBeats + 72 + Nudge + 0.07 * Math.Sin(Seconds * 2 * Math.PI / 3.7);

    public PhaseMeterDisplay MeterDisplay(PhaseMeterType style) =>
        PhaseMeterModel.Display(1, [Input(1, DeckBeats), Input(2, MasterBeats)], master: 2, style);

    private static PhaseMeterDeckInput Input(int number, double beats)
    {
        var bars = Math.Floor(beats / 4);
        var phase = beats - 4 * bars;
        return new PhaseMeterDeckInput(number, phase, new BarPosition((int)bars + 1, (int)phase + 1));
    }

    private double Nudge
    {
        get
        {
            var t = Seconds % Cycle;
            const double half = Cycle / 2;
            static double Ease(double x)
            {
                var clamped = Math.Clamp(x, 0, 1);
                return clamped * clamped * (3 - 2 * clamped);
            }
            return t < half ? 1 - Ease(t / NudgeDuration) : Ease((t - half) / NudgeDuration);
        }
    }
}

/// The waveform card's preview (WaveformPreview.swift): rekordbox's two stacked waveforms, split into halves with a
/// playhead each, or hidden.
public sealed class WaveformSketch : FrameworkElement
{
    private const double PointsPerBeat = 26;
    private static readonly Brush BeatLine = WindowTheme.White(0.22);
    private static readonly Brush Divider = WindowTheme.White(0.14);

    private WaveformMode _mode;
    private DeckPhases _phases;

    public WaveformMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            InvalidateVisual();
        }
    }

    public DeckPhases Phases
    {
        get => _phases;
        set
        {
            _phases = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext context)
    {
        double width = ActualWidth, height = ActualHeight;
        switch (_mode)
        {
            case WaveformMode.Normal:
            {
                DrawStrip(context, new Rect(0, 0, width, height / 2 - 1), _phases.One, WindowTheme.Deck);
                DrawStrip(context, new Rect(0, height / 2 + 1, width, height / 2 - 1), _phases.Two, WindowTheme.DeckTwo);
                Playhead(context, width / 2, 0, height);
                break;
            }
            case WaveformMode.Split:
            {
                const double gap = 6;
                var left = new Rect(0, 0, (width - gap) / 2, height);
                var right = new Rect(left.Right + gap, 0, left.Width, height);
                DrawStrip(context, left, _phases.One, WindowTheme.Deck);
                DrawStrip(context, right, _phases.Two, WindowTheme.DeckTwo);
                Playhead(context, left.X + left.Width / 2, 0, height);
                Playhead(context, right.X + right.Width / 2, 0, height);
                context.DrawRectangle(Divider, null, new Rect(left.Right + gap / 2 - 0.5, 4, 1, height - 8));
                break;
            }
        }
    }

    private static void DrawStrip(DrawingContext context, Rect rect, double beats, Brush color)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        context.PushClip(new RectangleGeometry(rect));
        var playheadX = rect.X + rect.Width / 2;
        var midY = rect.Y + rect.Height / 2;
        var bars = new StreamGeometry();
        using (var stream = bars.Open())
        {
            for (var x = rect.Left; x <= rect.Right; x += 2.4)
            {
                var u = beats + (x - playheadX) / PointsPerBeat;
                var barHeight = Math.Max(1.5, Amplitude(u) * rect.Height * 0.86);
                PhaseMeterCanvas.AddRect(stream, new Rect(x, midY - barHeight / 2, 1.4, barHeight));
            }
        }
        bars.Freeze();
        context.DrawGeometry(color, null, bars);

        var first = (int)Math.Floor(beats + (rect.Left - playheadX) / PointsPerBeat);
        var last = (int)Math.Ceiling(beats + (rect.Right - playheadX) / PointsPerBeat);
        for (var beat = first; beat <= last; beat++)
        {
            var tickX = playheadX + (beat - beats) * PointsPerBeat;
            context.DrawRectangle(BeatLine, null, new Rect(tickX - 0.5, rect.Y, 1, rect.Height));
            if (beat % 4 != 0) continue;
            var marker = new StreamGeometry();
            using (var stream = marker.Open())
            {
                stream.BeginFigure(new Point(tickX - 3, rect.Y), true, true);
                stream.PolyLineTo([new Point(tickX + 3, rect.Y), new Point(tickX, rect.Y + 4.5)], false, false);
            }
            marker.Freeze();
            context.DrawGeometry(WindowTheme.Downbeat, null, marker);
        }
        context.Pop();
    }

    private static void Playhead(DrawingContext context, double x, double top, double bottom) =>
        context.DrawRectangle(WindowTheme.WhiteBrush, null, new Rect(x - 1, top, 2, bottom - top));

    private static double Amplitude(double beats)
    {
        var fraction = beats - Math.Floor(beats);
        var kick = 0.22 + 0.78 * Math.Exp(-5 * fraction);
        var slice = (int)Math.Floor(beats * 10);
        return kick * (0.55 + 0.45 * Noise((slice % 40 + 40) % 40));
    }

    private static double Noise(int index)
    {
        unchecked
        {
            var hash = (uint)(index * 374_761_393 + 668_265_263);
            hash = (hash ^ (hash >> 13)) * 1_274_126_177;
            hash ^= hash >> 16;
            return (hash & 0xFFFF) / 65535.0;
        }
    }
}
