using Earmatch.Core;
using static Earmatch.Core.Tests.Fixture;

namespace Earmatch.Core.Tests;

/// The phase meters between captured frames (the meter part of OverlayController.swift): each frame anchors the
/// decks' predictors, the board draws them at each refresh, and a few frames that do not show the window as the
/// layout describes it are ridden out.
public class PhaseMeterBoardTests
{
    static DeckLayout Deck(int number, bool loaded = true, double? elapsed = 10) =>
        new(number, loaded ? $"Track {number}" : null, loaded ? 120 : null, null, null, null, Rect(100 * number, 0, 70, 13), null, null,
            elapsedSeconds: loaded ? elapsed : null);

    static RekordboxLayout Layout(params DeckLayout[] decks) => new(RekordboxMode.Performance, "2Deck Horizontal", null, decks);

    static DeckFrameReading Reading(double? phase, bool isMaster = false, double? cue = null, PhaseTrackerStatus status = PhaseTrackerStatus.Live) =>
        new(null, new PhaseEstimate(phase, phase is null ? null : 20, status), phase is null ? null : 2, isMaster, null, cue);

    static FrameAnalysis Frame(double time, Dictionary<int, DeckFrameReading> decks, FrameMappingProblem? problem = null) => new(decks, problem, time);

    static readonly OverlaySettings Settings = new() { IsPhaseMeterEnabled = true, PhaseMeterType = PhaseMeterType.Line };

    [Fact]
    public void TheSyncMasterIsTheDeckWhoseMasterButtonIsLit()
    {
        var board = new PhaseMeterBoard();
        var layout = Layout(Deck(1), Deck(2));

        Assert.True(board.Accept(Frame(1, new() { [1] = Reading(1.0), [2] = Reading(2.0, isMaster: true) }), layout, out var changed));
        Assert.False(changed);
        Assert.Equal(2, board.Master);
        Assert.True(board.HasFrame);
    }

    [Fact]
    public void EachLoadedDeckGetsAMeterAgainstTheMaster()
    {
        var board = new PhaseMeterBoard();
        var layout = Layout(Deck(1), Deck(2), Deck(3, loaded: false));
        board.Accept(Frame(1, new() { [1] = Reading(1.0), [2] = Reading(2.0, isMaster: true), [3] = Reading(null) }), layout, out _);

        Assert.True(board.Compute(layout, Settings, 1));

        var one = board.Display(1)!;
        Assert.Equal(2, one.MasterPlayer);
        Assert.Equal([PhaseMeterRole.Master, PhaseMeterRole.Deck], one.Rows.Select(row => row.Role));
        Assert.Equal(1, one.Rows[1].BeatInBar);
        Assert.Null(board.Display(2)!.MasterPlayer);
        Assert.Null(board.Display(3));
    }

    [Fact]
    public void TheMetersMoveOnAtTheMeasuredTempoBetweenFrames()
    {
        var board = new PhaseMeterBoard();
        var layout = Layout(Deck(1));
        board.Accept(Frame(1, new() { [1] = Reading(1.0) }), layout, out _);

        board.Compute(layout, Settings, 1);
        board.Compute(layout, Settings, 1.25);

        var row = board.Display(1)!.Rows[0];
        Assert.Equal(1, row.BeatInBar);
        Assert.Equal(0.5, row.Fraction!.Value, 2);
    }

    [Fact]
    public void ComputingAgainWithNothingNewChangesNothing()
    {
        var board = new PhaseMeterBoard();
        var layout = Layout(Deck(1, elapsed: null));
        board.Accept(Frame(1, new() { [1] = Reading(1.0, status: PhaseTrackerStatus.Held) with { BeatsPerSecond = 0 } }), layout, out _);

        Assert.True(board.Compute(layout, Settings, 1));
        Assert.False(board.Compute(layout, Settings, 1.01));
        board.Invalidate();
        Assert.True(board.Compute(layout, Settings, 1.02));
    }

    [Fact]
    public void AFewUnmappedFramesAreRiddenOutThenReported()
    {
        var board = new PhaseMeterBoard();
        var layout = Layout(Deck(1));
        var problem = new FrameMappingProblem(800, 600, 1600, 1200);
        board.Accept(Frame(1, new() { [1] = Reading(1.0) }), layout, out _);

        for (var frame = 0; frame < PhaseMeterBoard.UnmappedFramesTolerated; frame++)
        {
            Assert.False(board.Accept(Frame(1.1 + frame * 0.01, new() { [1] = Reading(1.0) }, problem), layout, out _));
            Assert.True(board.HasFrame);
        }
        Assert.True(board.Accept(Frame(1.5, new() { [1] = Reading(1.0) }, problem), layout, out var changed));
        Assert.True(changed);
        Assert.False(board.HasFrame);
        board.Compute(layout, Settings, 1.5);
        Assert.Null(board.Display(1));
    }

    /// A frame ridden out does not show the window as the layout describes it, so what was read from it is not a
    /// measurement: the meters keep moving on from the last good frame.
    [Fact]
    public void AFrameRiddenOutLeavesThePredictorsAlone()
    {
        var board = new PhaseMeterBoard();
        var layout = Layout(Deck(1));
        board.Accept(Frame(1, new() { [1] = Reading(1.0) }), layout, out _);

        Assert.False(board.Accept(Frame(1.1, new() { [1] = Reading(null) }, new FrameMappingProblem(800, 600, 1600, 1200)), layout, out _));
        Assert.False(board.Accept(Frame(1.2, new() { [1] = Reading(3.0) }, new FrameMappingProblem(800, 600, 1600, 1200)), layout, out _));
        board.Compute(layout, Settings, 1);
        board.Compute(layout, Settings, 1.25);

        var row = board.Display(1)!.Rows[0];
        Assert.Equal(1, row.BeatInBar);
        Assert.Equal(0.5, row.Fraction!.Value, 2);
    }

    /// While tracing, each frame measures how far the meters drawn at its capture time were from the phase it reads
    /// (measureMeterError in OverlayController.swift): 0.02 beats at 2 beats a second is 10 ms.
    [Fact]
    public void WhileTracingEachFrameMeasuresTheMeterError()
    {
        var stats = new FrameStats(isTracing: true);
        var board = new PhaseMeterBoard(stats);
        var layout = Layout(Deck(1));
        stats.Record(0.001, 0, new Dictionary<int, DeckFrameReading>());
        board.Accept(Frame(1, new() { [1] = Reading(1.0) }), layout, out _);
        board.Compute(layout, Settings, 1);
        board.Compute(layout, Settings, 1.25);

        board.Accept(Frame(1.25, new() { [1] = Reading(1.52) }), layout, out _);
        board.Accept(Frame(1.3, new() { [1] = Reading(1.6, status: PhaseTrackerStatus.Held) }), layout, out _);
        stats.Record(0.001, 1, new Dictionary<int, DeckFrameReading>());

        Assert.Equal(10, stats.Current.PredictionErrorMilliseconds!.Value, 3);
    }

    [Fact]
    public void WithoutTracingNoMeterErrorIsMeasured()
    {
        var stats = new FrameStats();
        var board = new PhaseMeterBoard(stats);
        var layout = Layout(Deck(1));
        stats.Record(0.001, 0, new Dictionary<int, DeckFrameReading>());
        board.Accept(Frame(1, new() { [1] = Reading(1.0) }), layout, out _);
        board.Compute(layout, Settings, 1.25);
        board.Accept(Frame(1.25, new() { [1] = Reading(1.52) }), layout, out _);
        stats.Record(0.001, 1, new Dictionary<int, DeckFrameReading>());

        Assert.Null(stats.Current.PredictionErrorMilliseconds);
    }

    [Fact]
    public void ResettingForgetsTheFramesAndSaysWhetherAProblemCleared()
    {
        var board = new PhaseMeterBoard();
        var layout = Layout(Deck(1));
        board.Accept(Frame(1, new() { [1] = Reading(1.0) }, new FrameMappingProblem(1, 1, 2, 2)), layout, out _);

        Assert.True(board.Reset());
        Assert.Null(board.Latest);
        Assert.Null(board.Master);
        Assert.False(board.Reset());
    }

    [Fact]
    public void TheCueCountdownCountsFromTheLatestFrame()
    {
        var board = new PhaseMeterBoard();
        var layout = Layout(Deck(1));
        board.Accept(Frame(1, new() { [1] = Reading(1.0, cue: 8.5) }), layout, out _);

        board.Compute(layout, Settings with { CounterStyle = CounterStyle.CueCountdown }, 1.1);

        Assert.Equal("02.1", board.Display(1)!.Rows[0].Counter);
    }

    [Fact]
    public void TheLiveSnapshotShowsOnlyTheLoadedDecksOfAFourDeckLayout()
    {
        var board = new PhaseMeterBoard();
        var layout = Layout(Deck(1), Deck(2, loaded: false), Deck(3), Deck(4, loaded: false));
        board.Accept(Frame(1, new() { [1] = Reading(1.0, isMaster: true), [3] = Reading(3.0) }), layout, out _);
        board.Compute(layout, Settings, 1);

        var snapshot = board.LiveSnapshot(layout, framesPerSecond: 58, latencyMilliseconds: 21.5, now: 1.1);

        Assert.Equal([1, 3], snapshot.Decks.Select(deck => deck.Number));
        Assert.True(snapshot.Decks[0].IsMaster);
        Assert.Equal(2, snapshot.Decks[0].BeatsPerSecond);
        Assert.Equal(1.2, snapshot.Decks[0].PhaseInBar!.Value, 6);
        Assert.NotNull(snapshot.Decks[1].Meter);
        Assert.Equal(58, snapshot.FramesPerSecond);
        Assert.Equal(21.5, snapshot.LatencyMilliseconds);
    }

    [Fact]
    public void AMappingProblemReadsAsTheMacOSMessageInPixels()
    {
        Assert.Equal("frame shows 800x600 px but the window is 1600x1200 px", L10n.English(() => new FrameMappingProblem(800, 600, 1600, 1200).Message));
    }
}
