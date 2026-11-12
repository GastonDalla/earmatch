namespace Earmatch.Core;

/// What the beat markers along one waveform strip say: the beat spacing in screen units, the markers on the grid (their
/// x, or y in a vertical strip), the downbeats among them and the playhead's position in the bar (0 to 4 beats).
public sealed record BeatGridReading(double BeatSpacing, ValueList<double> BeatXs, ValueList<double> DownbeatXs, double? PhaseInBar);

/// Reads the beat grid rekordbox draws along a waveform strip: grey ticks on the beats and red marks on the downbeats,
/// in a thin band at the strip's edge (both edges of a vertical column).
public static class BeatGridDetector
{
    public const double BeatsPerBar = 4.0;

    internal const double BandTop = 0.75;
    internal const double BandBottom = 4.5;
    internal const double ClippedMinimum = 3.0;

    public static BeatGridReading? Read(ScreenRect strip, double playheadX, PixelImage image) =>
        Read(new WaveformStrip(strip, StripOrientation.Horizontal), playheadX, image);

    public static BeatGridReading? Read(WaveformStrip strip, double playhead, PixelImage image)
    {
        var markers = FindMarkers(strip, playhead, image);
        if (markers.Count < 3 || FitGrid(markers, playhead, minimumSpacing: image.Points(1)) is not { } grid) return null;
        var onGrid = markers.Where(marker => grid.Fits(marker.X)).ToList();
        var downbeats = onGrid.Where(marker => marker.IsDownbeat).Select(marker => marker.X).ToList();
        return new BeatGridReading(grid.Spacing, onGrid.Select(marker => marker.X).ToValueList(), downbeats.ToValueList(),
            grid.Phase(playhead, downbeats));
    }

    internal readonly record struct Marker(double X, bool IsDownbeat);

    private record struct Run(int Start, int End, bool Red);

    internal static List<Marker> FindMarkers(WaveformStrip strip, double playhead, PixelImage image)
    {
        var area = strip.MarkerArea;
        var isVertical = strip.Orientation == StripOrientation.Vertical;
        var (linesStart, linesEnd) = isVertical ? (image.Row(area.MinY), image.Row(area.MaxY)) : (image.Column(area.MinX), image.Column(area.MaxX));
        var playheadLine = isVertical ? (playhead - image.Origin.Y) * image.Scale : (playhead - image.Origin.X) * image.Scale;
        (int Start, int End) near, far;
        if (isVertical)
        {
            near = (image.Column(area.MinX + image.Points(BandTop)), image.Column(area.MinX + image.Points(BandBottom)));
            far = (image.Column(area.MaxX - image.Points(BandBottom)), image.Column(area.MaxX - image.Points(BandTop)));
        }
        else
        {
            near = (image.Row(area.MinY + image.Points(BandTop)), image.Row(area.MinY + image.Points(BandBottom)));
            far = (0, 0);
        }
        if (linesStart >= linesEnd || near.Start >= near.End) return [];

        var playheadClearance = 2 * image.PixelsPerPoint;
        var markers = new List<Marker>();
        Run? run = null;

        void CloseRun(Run? finished = null)
        {
            if ((finished ?? run) is not { } current) return;
            run = null;
            var widthInPoints = (current.End - current.Start + 1) / image.PixelsPerPoint;
            var maxWidth = current.Red ? 9.0 : 4.0;
            if (widthInPoints > maxWidth || (current.Red && widthInPoints < 2)) return;
            var isCut = current.Start == linesStart || current.End == linesEnd - 1;
            if (isCut && widthInPoints < ClippedMinimum) return;
            var center = (current.Start + current.End + 1) / 2.0;
            markers.Add(new Marker(isVertical ? image.PointY(center) : image.PointX(center), current.Red));
        }

        for (var line = linesStart; line < linesEnd; line++)
        {
            var (reds, greys) = Count(line, near, isVertical, image);
            if (isVertical)
            {
                var other = Count(line, far, isVertical: true, image);
                reds = reds > 0 && other.Reds > 0 ? reds + other.Reds : 0;
                greys = greys > 0 && other.Greys > 0 ? greys + other.Greys : 0;
            }
            var isRed = reds >= 2;
            var isTick = !isRed && greys >= 2 && Math.Abs(line + 0.5 - playheadLine) > playheadClearance;
            if (isRed || isTick)
            {
                if (run is { } current && line - current.End <= 2)
                {
                    run = new Run(current.Start, line, current.Red || isRed);
                }
                else
                {
                    CloseRun();
                    run = new Run(line, line, isRed);
                }
            }
            else if (run is { } current && line - current.End > 2)
            {
                CloseRun(current);
            }
        }
        CloseRun();
        return markers;
    }

    private static (int Reds, int Greys) Count(int line, (int Start, int End) band, bool isVertical, PixelImage image)
    {
        int reds = 0, greys = 0;
        for (var position = band.Start; position < band.End; position++)
        {
            var color = isVertical ? image.Pixel(position, line) : image.Pixel(line, position);
            if (color.IsMarkerRed) reds++;
            else if (color.IsTickGrey) greys++;
        }
        return (reds, greys);
    }

    internal readonly record struct Grid(double Reference, double Spacing)
    {
        public double Beats(double x) => (x - Reference) / Spacing;

        public bool Fits(double x)
        {
            var k = Beats(x);
            return Math.Abs(k - Numbers.Round(k)) < 0.2;
        }

        /// The playhead's beat in the bar, counted from the downbeat residue most red marks agree on.
        public double? Phase(double x, IReadOnlyList<double> downbeats)
        {
            if (downbeats.Count == 0) return null;
            var votes = new int[4];
            foreach (var downbeat in downbeats) votes[Numbers.PositiveModulo((int)Numbers.Round(Beats(downbeat)), 4)]++;
            var residue = 0;
            for (var index = 1; index < votes.Length; index++)
            {
                if (votes[residue] < votes[index]) residue = index;
            }
            return Numbers.PositiveModulo(Beats(x) - residue, BeatsPerBar);
        }
    }

    /// A least-squares grid through the markers that agree with the typical gap, anchored on the best supported marker
    /// (the one nearest the playhead among equals). The gap must be longer than the minimum spacing (one point).
    internal static Grid? FitGrid(IReadOnlyList<Marker> markers, double playheadX, double minimumSpacing)
    {
        var xs = markers.Select(marker => marker.X).Order().ToList();
        var gaps = xs.Skip(1).Zip(xs, (next, previous) => next - previous).Order().ToList();
        var median = gaps[gaps.Count / 2];
        var similar = gaps.Where(gap => Math.Abs(gap / median - 1) < 0.15).ToList();
        if (!(median > minimumSpacing) || similar.Count == 0) return null;
        var sum = 0.0;
        foreach (var gap in similar) sum += gap;
        var estimate = sum / similar.Count;

        int Support(double candidate) => xs.Count(x =>
        {
            var k = (x - candidate) / estimate;
            return Math.Abs(k - Numbers.Round(k)) < 0.2;
        });

        var reference = xs[0];
        var referenceSupport = Support(reference);
        foreach (var x in xs.Skip(1))
        {
            var support = Support(x);
            if (referenceSupport != support ? referenceSupport < support : Math.Abs(reference - playheadX) > Math.Abs(x - playheadX))
            {
                reference = x;
                referenceSupport = support;
            }
        }

        double count = 0, sumK = 0, sumX = 0, sumKK = 0, sumKX = 0;
        foreach (var x in xs)
        {
            var position = (x - reference) / estimate;
            var k = Numbers.Round(position);
            if (Math.Abs(position - k) >= 0.2) continue;
            count += 1;
            sumK += k;
            sumX += x;
            sumKK += k * k;
            sumKX += k * x;
        }
        var denominator = count * sumKK - sumK * sumK;
        if (count < 2 || !(denominator > 0)) return null;
        var spacing = (count * sumKX - sumK * sumX) / denominator;
        return new Grid((sumX - spacing * sumK) / count, spacing);
    }
}

/// Finds the grey playhead line rekordbox draws across a strip, within 3 points of its centre.
public static class PlayheadDetector
{
    internal const double SearchDistance = 3;
    internal const double Coverage = 0.8;

    public static double? Locate(WaveformStrip strip, PixelImage image)
    {
        var isVertical = strip.Orientation == StripOrientation.Vertical;
        var area = strip.MarkerArea;
        var reach = image.Points(SearchDistance);
        var (candidatesStart, candidatesEnd) = isVertical
            ? (image.Row(strip.Center - reach), image.Row(strip.Center + reach))
            : (image.Column(strip.Center - reach), image.Column(strip.Center + reach));
        var (acrossStart, acrossEnd) = isVertical ? (image.Column(area.MinX), image.Column(area.MaxX)) : (image.Row(area.MinY), image.Row(area.MaxY));
        var acrossCount = acrossEnd - acrossStart;
        if (candidatesStart >= candidatesEnd || acrossCount < 8) return null;
        var total = (acrossCount + 1) / 2;
        var allowedMisses = total - (int)(total * Coverage);
        int? first = null;
        var last = 0;
        for (var line = candidatesStart; line < candidatesEnd; line++)
        {
            var misses = 0;
            for (var position = acrossStart; position < acrossEnd; position += 2)
            {
                var color = isVertical ? image.Pixel(position, line) : image.Pixel(line, position);
                if (color.IsPlayheadGrey) continue;
                misses++;
                if (misses > allowedMisses) break;
            }
            if (misses > allowedMisses)
            {
                if (first is not null) break;
                continue;
            }
            first ??= line;
            last = line;
        }
        if (first is not { } start) return null;
        var center = (start + last + 1) / 2.0;
        return isVertical ? image.PointY(center) : image.PointX(center);
    }
}

/// Tells the sync master by the blue border rekordbox draws on its MASTER button. Only the part of the button inside
/// the frame is read: a row or column outside it is skipped, never taken from the frame's edge.
public static class SyncMasterDetector
{
    public static bool IsMaster(ScreenRect button, PixelImage image)
    {
        var left = Math.Max(image.UnclampedColumn(button.MinX + image.Points(4)), 0);
        var right = Math.Min(image.UnclampedColumn(button.MaxX - image.Points(4)), image.Width);
        if (right <= left) return false;
        var rows = new[] { button.MinY - image.Points(1), button.MinY, button.MinY + image.Points(1), button.MaxY - image.Points(2),
            button.MaxY - image.Points(1), button.MaxY }.Select(image.UnclampedRow).Distinct();
        var best = 0;
        foreach (var row in rows.Where(row => row >= 0 && row < image.Height))
        {
            var blue = 0;
            for (var column = left; column < right; column++)
            {
                if (image.Pixel(column, row).IsMasterBlue) blue++;
            }
            best = Math.Max(best, blue);
        }
        return (double)best / (right - left) >= 0.5;
    }
}
