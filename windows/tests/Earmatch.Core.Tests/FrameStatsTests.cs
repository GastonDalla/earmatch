using Earmatch.Core;

namespace Earmatch.Core.Tests;

/// The capture's figures for the diagnostics and the live decks line (FrameStats in FrameAnalyzer.swift), measured
/// over windows of one second of frames.
public class FrameStatsTests
{
    static readonly IReadOnlyDictionary<int, DeckFrameReading> NoDecks = new Dictionary<int, DeckFrameReading>();

    [Fact]
    public void NothingIsReportedBeforeASecondOfFrames()
    {
        var stats = new FrameStats();
        for (var frame = 0; frame < 30; frame++) stats.Record(0.002, frame / 60.0, NoDecks);

        Assert.Equal(new FrameStats.Snapshot(), stats.Current);
    }

    [Fact]
    public void FramesPerSecondAndAnalysisTimeCoverTheLastSecond()
    {
        var stats = new FrameStats();
        for (var frame = 0; frame <= 60; frame++) stats.Record(frame % 2 == 0 ? 0.002 : 0.004, frame / 60.0, NoDecks);

        var snapshot = stats.Current;
        Assert.Equal(60, snapshot.FramesPerSecond, 6);
        Assert.Equal(3, snapshot.AverageProcessingMilliseconds, 1);
        Assert.Equal(4, snapshot.MaxProcessingMilliseconds, 6);
        Assert.Equal(0, snapshot.CaptureJitterMilliseconds, 6);
    }

    [Fact]
    public void TheCaptureToScreenLatencyIsTheAverageOfTheRefreshesSinceTheLastWindow()
    {
        var stats = new FrameStats();
        stats.Record(0.001, 0, NoDecks);
        stats.RecordScreen(earliest: 0.010, latest: 0.020);
        stats.RecordScreen(earliest: 0.020, latest: 0.040);
        stats.RecordRefresh(0.0001);
        stats.RecordRefresh(0.0003);
        stats.Record(0.001, 1, NoDecks);

        var snapshot = stats.Current;
        Assert.Equal(15, snapshot.AverageEarliestLatencyMilliseconds, 6);
        Assert.Equal(30, snapshot.AverageLatencyMilliseconds, 6);
        Assert.Equal(40, snapshot.MaxLatencyMilliseconds, 6);
        Assert.Equal(2, snapshot.RefreshesPerSecond, 6);
        Assert.Equal(200, snapshot.AverageRefreshMicroseconds, 6);
    }

    [Fact]
    public void JitterIsTheSpreadOfTheIntervalsBetweenFrames()
    {
        var stats = new FrameStats();
        var time = 0.0;
        for (var frame = 0; time < 1.1; frame++)
        {
            stats.Record(0.001, time, NoDecks);
            time += frame % 2 == 0 ? 0.010 : 0.020;
        }

        Assert.Equal(5, stats.Current.CaptureJitterMilliseconds, 1);
    }

    [Fact]
    public void EachWindowStartsAfresh()
    {
        var stats = new FrameStats();
        stats.Record(0.001, 0, NoDecks);
        stats.RecordScreen(0.010, 0.050);
        stats.Record(0.001, 1, NoDecks);
        stats.Record(0.001, 2, NoDecks);

        Assert.Equal(0, stats.Current.AverageLatencyMilliseconds);
        Assert.Equal(1, stats.Current.FramesPerSecond, 6);
    }

    /// The meter error (measured only while tracing, as on macOS) is the average distance between where the meters were
    /// drawn and where the next frames measured the decks; none without a measurement.
    [Fact]
    public void TheMeterErrorIsTheAverageOfTheMeasuredDistances()
    {
        var stats = new FrameStats(isTracing: true);
        stats.Record(0.001, 0, NoDecks);
        stats.RecordPredictionError(-0.002);
        stats.RecordPredictionError(0.004);
        stats.Record(0.001, 1, NoDecks);

        Assert.Equal(3, stats.Current.PredictionErrorMilliseconds!.Value, 6);
        stats.Record(0.001, 2, NoDecks);
        Assert.Null(stats.Current.PredictionErrorMilliseconds);
    }
}
