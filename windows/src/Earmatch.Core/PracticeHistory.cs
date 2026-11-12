namespace Earmatch.Core;

/// The saved mixes, oldest first, and the trace of the newest one.
public sealed class PracticeHistory
{
    public const int Capacity = 200;

    private readonly List<PracticeMix> _mixes;

    public PracticeHistory(IEnumerable<PracticeMix>? mixes = null, PracticeTrace? lastTrace = null)
    {
        _mixes = (mixes ?? []).OrderBy(mix => mix.EndedAt).TakeLast(Capacity).ToList();
        LastTrace = lastTrace;
    }

    public IReadOnlyList<PracticeMix> Mixes => _mixes;
    public PracticeTrace? LastTrace { get; private set; }

    public void Record(PracticeResult result)
    {
        var index = _mixes.FindLastIndex(mix => mix.EndedAt <= result.Mix.EndedAt) + 1;
        _mixes.Insert(index, result.Mix);
        if (_mixes.Count > Capacity) _mixes.RemoveRange(0, _mixes.Count - Capacity);
        LastTrace = result.Trace;
    }
}

public sealed record PracticeDay(DateTimeOffset Day, int MixCount, double? MedianPhaseErrorMilliseconds, double? MedianSecondsToMatch);

public static class PracticeProgress
{
    public const int DayLimit = 14;

    /// The median phase error and tempo match of each day with mixes, oldest first; days follow the time zone.
    public static ValueList<PracticeDay> Days(IReadOnlyList<PracticeMix> mixes, TimeZoneInfo zone, int limit = DayLimit) =>
        mixes.GroupBy(mix => StartOfDay(mix.EndedAt, zone))
            .OrderBy(group => group.Key)
            .TakeLast(limit)
            .Select(group => new PracticeDay(group.Key, group.Count(),
                Median(group.Select(mix => mix.MeanPhaseErrorMilliseconds)),
                Median(group.Select(mix => mix.SecondsToMatch))))
            .ToValueList();

    /// Midnight of the day the moment falls on in the time zone.
    public static DateTimeOffset StartOfDay(DateTimeOffset moment, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(moment, zone).DateTime.Date;
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    internal static double? Median(IEnumerable<double?> values)
    {
        var sorted = values.OfType<double>().Order().ToList();
        if (sorted.Count == 0) return null;
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
    }
}

public sealed record PracticeOverview(PracticeMix? LastMix, PracticeTrace? LastTrace, int MixCount, ValueList<PracticeDay> Days)
{
    public static PracticeOverview Empty { get; } = new(null, null, 0, []);

    public static PracticeOverview From(PracticeHistory history, TimeZoneInfo zone) =>
        new(history.Mixes.Count > 0 ? history.Mixes[^1] : null, history.LastTrace, history.Mixes.Count, PracticeProgress.Days(history.Mixes, zone));
}
