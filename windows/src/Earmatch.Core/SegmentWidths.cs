namespace Earmatch.Core;

public static class SegmentWidths
{
    public static double[] Distribute(IReadOnlyList<double> ideal, double available)
    {
        if (ideal.Count == 0) return [];
        available = Math.Max(available, 0);
        var total = ideal.Sum();
        if (total > available)
        {
            if (total <= 0) return ideal.Select(_ => available / ideal.Count).ToArray();
            return ideal.Select(width => width * available / total).ToArray();
        }
        var remaining = available;
        var keptCount = 0;
        foreach (var width in ideal.OrderByDescending(width => width))
        {
            var fairShare = remaining / (ideal.Count - keptCount);
            if (width <= fairShare) break;
            remaining -= width;
            keptCount++;
        }
        var share = keptCount == ideal.Count ? 0 : remaining / (ideal.Count - keptCount);
        return ideal.Select(width => Math.Max(width, share)).ToArray();
    }
}
