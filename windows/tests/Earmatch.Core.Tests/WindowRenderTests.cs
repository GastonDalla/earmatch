using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class WindowRenderRequestTests
{
    static WindowRenderRequest? Parse(params string[] arguments) => WindowRenderRequest.Parse(arguments);

    [Fact]
    public void WithoutTheFlagTheAppStartsNormally()
    {
        Assert.Null(Parse("Earmatch.exe"));
        Assert.Null(Parse("Earmatch.exe", "--dump-uia"));
    }

    [Fact]
    public void TheFlagNamesTheOutputDirectoryAndDefaultsToTheDefaultWindowSize()
    {
        var request = Assert.IsType<WindowRenderRequest>(Parse("Earmatch.exe", "--render-window", "/tmp/renders"));
        Assert.Equal(Path.GetFullPath("/tmp/renders"), request.Directory);
        Assert.Equal(MainWindowMetrics.DefaultSize, request.Size);
    }

    [Fact]
    public void ARelativeDirectoryIsReadFromTheCurrentDirectory()
    {
        var request = Assert.IsType<WindowRenderRequest>(Parse("Earmatch.exe", "--render-window", "out"));
        Assert.Equal(Path.GetFullPath("out"), request.Directory);
    }

    [Fact]
    public void TheMinimumSizeCanBeRequested()
    {
        var request = Assert.IsType<WindowRenderRequest>(Parse("Earmatch.exe", "--render-window", "out", "--render-size", "minimum"));
        Assert.Equal(MainWindowMetrics.MinimumSize, request.Size);
    }

    [Fact]
    public void AnExplicitSizeIsReadAsWidthByHeight()
    {
        var request = Assert.IsType<WindowRenderRequest>(Parse("Earmatch.exe", "--render-size", "1200x760", "--render-window", "out"));
        Assert.Equal(new WindowSize(1200, 760), request.Size);
    }

    [Fact]
    public void AMissingDirectoryIsAnError()
    {
        Assert.Equal(WindowRenderProblemKind.MissingDirectory, Assert.Throws<WindowRenderProblem>(() => Parse("Earmatch.exe", "--render-window")).Kind);
        Assert.Equal(WindowRenderProblemKind.MissingDirectory,
            Assert.Throws<WindowRenderProblem>(() => Parse("Earmatch.exe", "--render-window", "--render-size", "minimum")).Kind);
    }

    [Theory]
    [InlineData("huge")]
    [InlineData("1200")]
    [InlineData("x700")]
    [InlineData("1200x")]
    [InlineData("0x700")]
    [InlineData("-5x700")]
    [InlineData("1200x700x3")]
    [InlineData(" 1200x700")]
    public void AnUnreadableSizeIsAnError(string size)
    {
        var problem = Assert.Throws<WindowRenderProblem>(() => Parse("Earmatch.exe", "--render-window", "out", "--render-size", size));
        Assert.Equal(WindowRenderProblemKind.InvalidSize, problem.Kind);
        Assert.Equal(size, problem.Size);
    }

    [Fact]
    public void AMissingSizeIsAnError()
    {
        var problem = Assert.Throws<WindowRenderProblem>(() => Parse("Earmatch.exe", "--render-window", "out", "--render-size"));
        Assert.Equal(WindowRenderProblemKind.InvalidSize, problem.Kind);
        Assert.Equal("", problem.Size);
    }

    [Fact]
    public void ASizeBelowTheMinimumIsAnError()
    {
        var problem = Assert.Throws<WindowRenderProblem>(() => Parse("Earmatch.exe", "--render-window", "out", "--render-size", "400x300"));
        Assert.Equal(WindowRenderProblemKind.BelowMinimum, problem.Kind);
        Assert.Equal("400x300", problem.Size);
    }

    [Fact]
    public void EveryProblemExplainsTheUsage()
    {
        var problems = new[]
        {
            new WindowRenderProblem(WindowRenderProblemKind.MissingDirectory),
            new WindowRenderProblem(WindowRenderProblemKind.InvalidSize, "huge"),
            new WindowRenderProblem(WindowRenderProblemKind.BelowMinimum, "400x300"),
        };
        Assert.All(problems, problem => Assert.Contains("--render-window <directory>", problem.Message));
        Assert.Contains("\"huge\"", problems[1].Message);
    }
}

public class WindowRenderShotTests
{
    [Fact]
    public void OneRenderPerPanePlusTheFourDeckLiveView()
    {
        Assert.Equal(["live.png", "overlay.png", "practice.png", "setup.png", "live-4deck.png"], WindowRenderShot.All.Select(shot => shot.FileName));
        Assert.Equal([MainWindowPane.Live, MainWindowPane.Overlay, MainWindowPane.Practice, MainWindowPane.Setup, MainWindowPane.Live],
            WindowRenderShot.All.Select(shot => shot.Pane));
        Assert.Equal([2, 2, 2, 2, 4], WindowRenderShot.All.Select(shot => shot.DeckCount));
    }

    [Fact]
    public void RendersAreTwiceThePointSize()
    {
        Assert.Equal(2, WindowRenderShot.Scale);
    }

    [Fact]
    public void TheWindowFitsA1366By768Laptop()
    {
        // 1366x768 is the smallest common Windows laptop screen; the taskbar takes 48 px.
        var usable = new WindowSize(1366, 768 - 48);
        Assert.True(MainWindowMetrics.DefaultSize.Width <= usable.Width);
        Assert.True(MainWindowMetrics.DefaultSize.Height <= usable.Height);
        Assert.True(MainWindowMetrics.MinimumSize.Width <= MainWindowMetrics.DefaultSize.Width);
        Assert.True(MainWindowMetrics.MinimumSize.Height <= MainWindowMetrics.DefaultSize.Height);
        Assert.True(MainWindowMetrics.SidebarWidth < MainWindowMetrics.MinimumSize.Width / 3);
        Assert.Equal(new WindowSize(1040, 700), MainWindowMetrics.DefaultSize);
        Assert.Equal(new WindowSize(940, 640), MainWindowMetrics.MinimumSize);
    }
}

public class WindowRenderSampleTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void TheSampleDecksAreLoadedPlayingAndFollowOneMaster(int count)
    {
        var snapshot = WindowRenderSample.Decks(count);
        Assert.Equal(Enumerable.Range(1, count), snapshot.Decks.Select(deck => deck.Number));
        var master = Assert.Single(snapshot.Decks, deck => deck.IsMaster);
        Assert.All(snapshot.Decks, deck => Assert.True(deck.Title is not null && deck.Meter is not null && deck.IsPlaying));
        Assert.True(snapshot.FramesPerSecond > 0);
        Assert.True(snapshot.LatencyMilliseconds > 0);
        foreach (var deck in snapshot.Decks.Where(deck => !deck.IsMaster))
        {
            Assert.Equal(master.Number, deck.Meter?.MasterPlayer);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void TheSampleStatusReadsLiveWithFramesAndLatency(int count)
    {
        var context = WindowRenderSample.StatusContext(count);
        var line = L10n.English(() => StatusText.LiveDecks(context, WindowRenderSample.Decks(count)));
        Assert.Equal(StatusTone.Live, line.Tone);
        Assert.StartsWith("Live · ", line.Text);
        Assert.EndsWith(" ms", line.Text);
        Assert.Equal(count == 4 ? "4Deck Horizontal" : "2Deck Horizontal", context.LayoutName);
        Assert.Equal(StatusTone.Live, StatusText.Connection(context).Tone);
        Assert.Equal("rekordbox 7.2.4 connected · 2Deck Horizontal", L10n.English(() => StatusText.Connection(WindowRenderSample.StatusContext(2)).Text));
    }

    [Fact]
    public void TheSampleSettingsAreTheCDJ3000BoothWithEverythingOn()
    {
        var settings = WindowRenderSample.Settings;
        Assert.Equal(BoothPreset.Cdj3000, BoothPreset.Selected(settings));
        Assert.True(settings.IsEnabled && settings.IsMixTrainerEnabled && settings.IsPhaseMeterEnabled && settings.IsBPMLimitEnabled);
    }

    static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    [Fact]
    public void TheSamplePracticeHistoryEndsTodayWithAReplayOfTheLastMix()
    {
        var zone = TimeZoneInfo.Local;
        var overview = WindowRenderSample.PracticeOverview(Now, zone);
        var mix = Assert.IsType<PracticeMix>(overview.LastMix);
        var trace = Assert.IsType<PracticeTrace>(overview.LastTrace);
        Assert.Equal(mix.Id, trace.MixID);
        Assert.NotEmpty(trace.Samples);
        Assert.NotNull(trace.MatchedAtSeconds);
        Assert.True(overview.Days.Count >= 7);
        Assert.True(overview.MixCount >= overview.Days.Count);
        Assert.Equal(TimeZoneInfo.ConvertTime(Now, zone).Date, TimeZoneInfo.ConvertTime(overview.Days[^1].Day, zone).Date);
        Assert.True(mix.EndedAt <= Now);
    }

    [Fact]
    public void TheSamplePracticeHistoryIsTheSameEveryRun()
    {
        var zone = TimeZoneInfo.Local;
        Assert.Equal(WindowRenderSample.PracticeOverview(Now, zone), WindowRenderSample.PracticeOverview(Now, zone));
    }

    [Fact]
    public void TheSampleLiveMixIsTempoMatchedAndInPhase()
    {
        var live = WindowRenderSample.PracticeLive;
        Assert.True(live.IsMatched);
        Assert.True(live.IsLongEnough);
        var error = Assert.IsType<double>(live.PhaseErrorMilliseconds);
        Assert.True(Math.Abs(error) < 20);
    }
}
