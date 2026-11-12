namespace Earmatch.Core;

public sealed record BPMGlyphs(ValueList<ScreenRect> IntegerDigits, ScreenRect Point, ValueList<ScreenRect> Decimals);

public static class BPMGlyphMask
{
    public static ScreenRect Cover(BPMLimit limit, ScreenRect jogFrame, PixelImage image)
    {
        if (limit == BPMLimit.CoverFully || Glyphs(jogFrame, image) is not { Decimals.Count: > 0 } glyphs) return jogFrame;
        var last = glyphs.Decimals[^1];
        double left;
        if (limit == BPMLimit.OneDecimal)
        {
            var previous = glyphs.Decimals.Count >= 2 ? glyphs.Decimals[^2] : glyphs.Point;
            left = (previous.MaxX + last.MinX) / 2;
        }
        else
        {
            left = (glyphs.IntegerDigits[^1].MaxX + glyphs.Point.MinX) / 2;
        }
        var right = Math.Max(jogFrame.MaxX, last.MaxX + image.PointsInScreenUnits(1));
        return new ScreenRect(left, jogFrame.MinY, right - left, jogFrame.Height);
    }

    public static BPMGlyphs? Glyphs(ScreenRect jogFrame, PixelImage image)
    {
        var clusters = InkClusters(jogFrame, image);
        if (clusters.Count == 0) return null;
        var tallest = clusters.Max(cluster => cluster.Height);
        if (tallest < 4) return null;
        var capTop = clusters.Min(cluster => cluster.Top);
        bool IsLarge(Cluster cluster) => cluster.Top - capTop <= 0.25 * tallest;

        var lastLarge = clusters.FindLastIndex(IsLarge);
        if (lastLarge < 0 || !clusters.Take(lastLarge + 1).All(IsLarge)) return null;
        var rest = clusters.Skip(lastLarge + 1).ToList();
        if (rest.Count == 0 || !(rest[0].Height <= 0.35 * tallest)) return null;
        var point = rest[0];
        var decimals = rest.Skip(1).ToList();
        if (decimals.Count == 0 || !decimals.All(cluster => !IsLarge(cluster) && cluster.Height > 0.35 * tallest)) return null;
        return new BPMGlyphs(
            clusters.Take(lastLarge + 1).Select(cluster => cluster.Rect(image)).ToValueList(),
            point.Rect(image),
            decimals.Select(cluster => cluster.Rect(image)).ToValueList());
    }

    internal record struct Cluster(int First, int Last, int Top, int Bottom)
    {
        public readonly int Height => Bottom - Top + 1;

        public readonly ScreenRect Rect(PixelImage image)
        {
            double minX = image.ScreenX(First), maxX = image.ScreenX(Last + 1);
            double minY = image.ScreenY(Top), maxY = image.ScreenY(Bottom + 1);
            return new ScreenRect(minX, minY, maxX - minX, maxY - minY);
        }
    }

    internal static List<Cluster> InkClusters(ScreenRect frame, PixelImage image)
    {
        int left = image.Column(frame.MinX), right = image.Column(frame.MaxX);
        int top = image.Row(frame.MinY), bottom = image.Row(frame.MaxY);
        var clusters = new List<Cluster>();
        if (right <= left || bottom <= top) return clusters;
        Cluster? current = null;
        for (var x = left; x < right; x++)
        {
            int inkTop = -1, inkBottom = -1;
            for (var y = top; y < bottom; y++)
            {
                if (!image.Pixel(x, y).IsTextInk) continue;
                if (inkTop < 0) inkTop = y;
                inkBottom = y;
            }
            if (inkTop >= 0)
            {
                current = current is { } cluster
                    ? cluster with { Last = x, Top = Math.Min(cluster.Top, inkTop), Bottom = Math.Max(cluster.Bottom, inkBottom) }
                    : new Cluster(x, x, inkTop, inkBottom);
            }
            else if (current is { } cluster)
            {
                clusters.Add(cluster);
                current = null;
            }
        }
        if (current is { } last) clusters.Add(last);
        return clusters;
    }
}

internal static class TextInkColors
{
    extension(PixelImage.RGB color)
    {
        public bool IsTextInk => Math.Max(color.R, Math.Max(color.G, color.B)) >= 100;
    }
}
