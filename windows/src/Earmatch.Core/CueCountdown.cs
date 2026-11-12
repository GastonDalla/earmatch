namespace Earmatch.Core;

public enum CueMarkerKind
{
    HotCue,
    CuePoint,
}

/// A cue rekordbox draws on a horizontal waveform strip: a pink hot cue flag at the top or an orange cue point
/// triangle at the bottom, at X in screen units.
public readonly record struct CueMarker(double X, CueMarkerKind Kind);

/// Finds the cues ahead of the playhead and counts down to the nearest one in bars and beats, as the players do.
public static class CueCountdown
{
    internal static readonly (double Min, double Max) FlagWidth = (6, 16);
    internal static readonly (double Min, double Max) TriangleWidth = (6, 22);
    internal const double ReachedDistance = 1.0;

    /// The cues right of minX, left to right (a hot cue before a cue point at the same place).
    public static ValueList<CueMarker> Markers(ScreenRect strip, double minX, PixelImage image)
    {
        var left = image.Column(Math.Max(strip.MinX, minX - image.Points(8)));
        var right = image.Column(strip.MaxX);
        if (right <= left) return [];
        var found = new List<CueMarker>();

        var flagTop = image.Row(strip.MinY + image.Points(0.5));
        var flagBottom = image.Row(strip.MinY + image.Points(3));
        var flagBody = image.Row(strip.MinY + image.Points(9.5));
        foreach (var run in Runs(left, right, flagTop, flagBottom, image, color => color.IsHotCuePink))
        {
            if (Contains(FlagWidth, run.Width(image)) && Fill(run, flagBody, image, color => color.IsHotCuePink) >= 0.5)
                found.Add(new CueMarker(run.Center(image), CueMarkerKind.HotCue));
        }

        var baseTop = image.Row(strip.MaxY - image.Points(4));
        var baseBottom = image.Row(strip.MaxY - image.Points(0.5));
        var narrow = image.Row(strip.MaxY - image.Points(9));
        foreach (var run in Runs(left, right, baseTop, baseBottom, image, color => color.IsCueOrange))
        {
            if (!Contains(TriangleWidth, run.Width(image))) continue;
            var higher = Fill(run, narrow, image, color => color.IsCueOrange);
            if (higher > 0 && higher <= 0.6) found.Add(new CueMarker(run.Center(image), CueMarkerKind.CuePoint));
        }
        return found
            .Where(marker => marker.X > minX)
            .OrderBy(marker => marker.X)
            .ThenBy(marker => marker.Kind == CueMarkerKind.HotCue ? 0 : 1)
            .ToValueList();
    }

    /// How many beats ahead the nearest cue past the playhead is; none without a cue ahead or a usable beat spacing.
    public static double? BeatsToNextCue(ScreenRect strip, double playheadX, double beatSpacing, PixelImage image)
    {
        if (!(beatSpacing > image.Points(1))) return null;
        var ahead = Markers(strip, playheadX + image.Points(ReachedDistance), image);
        return ahead.Count > 0 ? (ahead[0].X - playheadX) / beatSpacing : null;
    }

    /// The countdown the players show: "00.1" on the last beat before the cue, "01.1" five beats before.
    public static BarPosition? Counter(double? beatsAhead)
    {
        if (beatsAhead is not { } ahead || !(ahead > 0)) return null;
        var beats = (int)Math.Ceiling(ahead - 1e-9);
        if (beats < 1) return null;
        return new BarPosition((beats - 1) / 4, (beats - 1) % 4 + 1);
    }

    /// The beats ahead a frame measured, moved on at the deck's tempo to the given time.
    public static double BeatsAhead(double measured, double measuredAt, double beatsPerSecond, double time) =>
        measured - beatsPerSecond * (time - measuredAt);

    internal readonly record struct Run(int First, int Last)
    {
        public double Width(PixelImage image) => (Last - First + 1) / image.PixelsPerPoint;

        public double Center(PixelImage image) => image.PointX((First + Last + 1) / 2.0);
    }

    private static bool Contains((double Min, double Max) range, double value) => value >= range.Min && value <= range.Max;

    /// Runs of columns with at least two matching pixels between the rows, joined across one-column gaps.
    internal static List<Run> Runs(int firstColumn, int endColumn, int firstRow, int endRow, PixelImage image, Func<PixelImage.RGB, bool> matches)
    {
        var result = new List<Run>();
        (firstColumn, endColumn) = (Math.Max(firstColumn, 0), Math.Min(endColumn, image.Width));
        (firstRow, endRow) = (Math.Max(firstRow, 0), Math.Min(endRow, image.Height));
        if (firstRow >= endRow) return result;
        Run? current = null;
        for (var x = firstColumn; x < endColumn; x++)
        {
            var count = 0;
            for (var y = firstRow; y < endRow; y++)
            {
                if (!matches(image.Pixel(x, y))) continue;
                count++;
                if (count >= 2) break;
            }
            if (count < 2) continue;
            if (current is { } run && x - run.Last <= 1)
            {
                current = run with { Last = x };
            }
            else
            {
                if (current is { } finished) result.Add(finished);
                current = new Run(x, x);
            }
        }
        if (current is { } last) result.Add(last);
        return result;
    }

    internal static double Fill(Run run, int row, PixelImage image, Func<PixelImage.RGB, bool> matches)
    {
        if (row < 0 || row >= image.Height) return 0;
        var matching = 0;
        for (var x = run.First; x <= run.Last; x++)
        {
            if (matches(image.Pixel(x, row))) matching++;
        }
        return (double)matching / (run.Last - run.First + 1);
    }
}

internal static class CueColors
{
    extension(PixelImage.RGB color)
    {
        public bool IsHotCuePink => color.R >= 200 && color.G is >= 25 and <= 120 && color.B is >= 85 and <= 165 && color.R - color.B >= 80;

        public bool IsCueOrange => color.R >= 200 && color.G is >= 130 and <= 200 && color.B <= 110 && color.R - color.G >= 35;
    }
}
