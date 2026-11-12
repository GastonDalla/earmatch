using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class PracticeHistoryTests
{
    const double Day = 86_400;
    static readonly DateTimeOffset FirstDay = DateTimeOffset.FromUnixTimeSeconds(1_793_836_800);
    static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    static PracticeMix Mix(DateTimeOffset end, double? error = 10, double? match = 5) =>
        new(Guid.NewGuid(), end.AddSeconds(-60), end, OutgoingDeck: 1, IncomingDeck: 2, BothPlayingSeconds: 60, SecondsToMatch: match,
            TempoDriftBPM: match is null ? null : 0.01, MeanPhaseErrorMilliseconds: error, WorstPhaseErrorMilliseconds: error * 2);

    static PracticeResult Result(DateTimeOffset end, double? error = 10)
    {
        var mix = Mix(end, error);
        return new PracticeResult(mix, new PracticeTrace(mix.Id, 60, 5, [new PracticeTraceSample(0, 4)]));
    }

    [Fact]
    public void RecordingAMixKeepsItAndItsTrace()
    {
        var history = new PracticeHistory();
        var result = Result(FirstDay);

        history.Record(result);

        Assert.Equal([result.Mix], history.Mixes);
        Assert.Equal(result.Trace, history.LastTrace);
    }

    [Fact]
    public void OnlyTheNewestTwoHundredMixesAreKept()
    {
        var history = new PracticeHistory();
        var results = Enumerable.Range(0, 205).Select(index => Result(FirstDay.AddSeconds(index * 120))).ToList();

        foreach (var result in results) history.Record(result);

        Assert.Equal(PracticeHistory.Capacity, history.Mixes.Count);
        Assert.Equal(results[5].Mix, history.Mixes[0]);
        Assert.Equal(results[204].Mix, history.Mixes[^1]);
        Assert.Equal(results[204].Trace, history.LastTrace);
    }

    [Fact]
    public void MixesStayInTheOrderTheyEnded()
    {
        var history = new PracticeHistory();
        var late = Result(FirstDay.AddSeconds(600));
        var early = Result(FirstDay);

        history.Record(late);
        history.Record(early);

        Assert.Equal([early.Mix, late.Mix], history.Mixes);
        Assert.Equal(early.Trace, history.LastTrace);
    }

    [Fact]
    public void ProgressIsTheMedianPhaseErrorOfEachDayOldestFirst()
    {
        var mixes = new[]
        {
            Mix(FirstDay.AddSeconds(3_600), error: 30),
            Mix(FirstDay.AddSeconds(7_200), error: 10),
            Mix(FirstDay.AddSeconds(9_000), error: 14),
            Mix(FirstDay.AddSeconds(2 * Day + 60), error: 8),
            Mix(FirstDay.AddSeconds(2 * Day + 120), error: null, match: null),
            Mix(FirstDay.AddSeconds(2 * Day + 180), error: 6),
        };

        var days = PracticeProgress.Days(mixes.Reverse().ToList(), Utc);

        Assert.Equal([FirstDay, FirstDay.AddSeconds(2 * Day)], days.Select(day => day.Day));
        Assert.Equal([3, 3], days.Select(day => day.MixCount));
        Assert.Equal([14.0, 7.0], days.Select(day => day.MedianPhaseErrorMilliseconds));
        Assert.Equal([5.0, 5.0], days.Select(day => day.MedianSecondsToMatch));
    }

    [Fact]
    public void ProgressKeepsTheMostRecentDays()
    {
        var mixes = Enumerable.Range(0, 20).Select(index => Mix(FirstDay.AddSeconds(index * Day + 60))).ToList();

        var days = PracticeProgress.Days(mixes, Utc, limit: 14);

        Assert.Equal(14, days.Count);
        Assert.Equal(FirstDay.AddSeconds(6 * Day), days[0].Day);
        Assert.Equal(FirstDay.AddSeconds(19 * Day), days[^1].Day);
    }

    [Fact]
    public void DaysFollowTheLocalCalendar()
    {
        var madrid = TimeZoneInfo.CreateCustomTimeZone("UTC+1", TimeSpan.FromHours(1), "UTC+1", "UTC+1");
        var days = PracticeProgress.Days([Mix(FirstDay.AddSeconds(Day - 1_800))], madrid);
        Assert.Equal(FirstDay.AddSeconds(Day - 3_600), Assert.Single(days).Day);
    }

    [Fact]
    public void TheOverviewLeadsWithTheNewestMixAndItsTrace()
    {
        var history = new PracticeHistory();
        var first = Result(FirstDay);
        var second = Result(FirstDay.AddSeconds(300), error: 20);
        history.Record(first);
        history.Record(second);

        var overview = PracticeOverview.From(history, Utc);

        Assert.Equal(second.Mix, overview.LastMix);
        Assert.Equal(second.Trace, overview.LastTrace);
        Assert.Equal(2, overview.MixCount);
        Assert.Equal([15.0], overview.Days.Select(day => day.MedianPhaseErrorMilliseconds));
        Assert.Null(PracticeOverview.Empty.LastMix);
    }
}

public class PracticeTraceTests
{
    static readonly PracticeTrace Trace = new(Guid.Empty, 10, 4,
    [
        new PracticeTraceSample(0, 40), new PracticeTraceSample(2, -30), new PracticeTraceSample(4, 6),
        new PracticeTraceSample(6, -12), new PracticeTraceSample(8, 3), new PracticeTraceSample(10, 1),
    ]);

    [Fact]
    public void TheErrorBetweenTwoSamplesIsInterpolated()
    {
        Assert.Equal(5, Trace.ErrorMilliseconds(1));
        Assert.Equal(-3, Trace.ErrorMilliseconds(5));
        Assert.Equal(6, Trace.ErrorMilliseconds(4));
    }

    [Fact]
    public void BeforeTheFirstOrAfterTheLastSampleTheEdgeHolds()
    {
        Assert.Equal(40, Trace.ErrorMilliseconds(-1));
        Assert.Equal(1, Trace.ErrorMilliseconds(30));
        Assert.Null((Trace with { Samples = [] }).ErrorMilliseconds(3));
    }

    [Fact]
    public void TheWorstSampleIsTheLargestErrorAfterTheTemposMatched()
    {
        Assert.Equal(new PracticeTraceSample(6, -12), Trace.WorstSample);
        Assert.Null((Trace with { MatchedAtSeconds = null }).WorstSample);
    }

    [Fact]
    public void ALiveMixIsLongEnoughAfterEightSecondsTogether()
    {
        var live = new PracticeLiveStatus(2, 1, 8, 7.9, null, null, null, null);
        Assert.False(live.IsLongEnough);
        Assert.False(live.IsMatched);
        Assert.True((live with { BothPlayingSeconds = 8, SecondsToMatch = 3 }).IsLongEnough);
        Assert.True((live with { SecondsToMatch = 3 }).IsMatched);
    }
}
