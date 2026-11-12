using Earmatch.Core;
using static Earmatch.Core.Tests.Fixture;

namespace Earmatch.Core.Tests;

/// What the rekordbox monitor keeps between full reads of the window (LiveWindowReader in RekordboxMonitor.swift): the
/// located layout, refreshed from the header BPMs and the elapsed and remaining times it re-reads on every poll.
public class LiveLayoutTests
{
    const string TwoDeckHorizontal = "uia-7.2.19-perf-2deck-horizontal.json";
    const string FourDeckHorizontal = "uia-7.2.19-perf-4deck-horizontal.json";

    static int IndexOf(RekordboxWindowSnapshot snapshot, ScreenRect frame) =>
        snapshot.Elements.Select((element, index) => (element, index)).Single(pair => pair.element.Frame == frame).index;

    [Fact]
    public void WithNothingReadAgainItIsTheLocatedLayout()
    {
        var live = new LiveLayout(Snapshot(TwoDeckHorizontal));

        Assert.Equal(Layout(TwoDeckHorizontal), live.Layout);
        Assert.Equal(Layout(TwoDeckHorizontal), live.Refresh(new Dictionary<int, string>()));
    }

    [Fact]
    public void ItWatchesTheHeaderBPMsAndTheTimes()
    {
        var snapshot = Snapshot(TwoDeckHorizontal);

        var live = new LiveLayout(snapshot);

        var watched = live.WatchedElements.Select(index => snapshot.Elements[index].Value).ToList();
        Assert.Equal(["130.00", "-00:07", ".3", "00:00", ".0", "140.00", "-00:13", ".7", "00:00", ".0"], watched);
    }

    [Fact]
    public void AHeaderBPMReadAgainChangesItsDeckOnly()
    {
        var snapshot = Snapshot(TwoDeckHorizontal);
        var live = new LiveLayout(snapshot);

        var layout = live.Refresh(new Dictionary<int, string> { [IndexOf(snapshot, Rect(358, 490, 88, 28))] = "131.50" });

        Assert.Equal([131.5, 140.0], layout.Decks.Select(deck => deck.BPM));
        Assert.Equal(130.0, live.Layout.Decks[0].BPM);
    }

    [Fact]
    public void AnEmptyHeaderBPMClearsTheBPM()
    {
        var snapshot = Snapshot(TwoDeckHorizontal);
        var live = new LiveLayout(snapshot);

        var layout = live.Refresh(new Dictionary<int, string> { [IndexOf(snapshot, Rect(1384, 490, 88, 28))] = "" });

        Assert.Null(layout.Decks[1].BPM);
    }

    [Fact]
    public void TimesReadAgainMoveTheElapsedAndRemainingSeconds()
    {
        var snapshot = Snapshot(TwoDeckHorizontal);
        var live = new LiveLayout(snapshot);

        var layout = live.Refresh(new Dictionary<int, string>
        {
            [IndexOf(snapshot, Rect(416, 486, 92, 32))] = "-00:05",
            [IndexOf(snapshot, Rect(508, 486, 24, 32))] = ".1",
            [IndexOf(snapshot, Rect(532, 486, 86, 32))] = "00:02",
            [IndexOf(snapshot, Rect(618, 486, 24, 32))] = ".2",
        });

        Assert.Equal(2.2, layout.Decks[0].ElapsedSeconds);
        Assert.Equal(5.1, layout.Decks[0].RemainingSeconds);
        Assert.Equal(13.7, layout.Decks[1].RemainingSeconds);
    }

    [Fact]
    public void LaterReadsBuildOnTheTimesReadBefore()
    {
        var snapshot = Snapshot(TwoDeckHorizontal);
        var live = new LiveLayout(snapshot);
        live.Refresh(new Dictionary<int, string> { [IndexOf(snapshot, Rect(532, 486, 86, 32))] = "00:02" });

        var layout = live.Refresh(new Dictionary<int, string> { [IndexOf(snapshot, Rect(618, 486, 24, 32))] = ".5" });

        Assert.Equal(2.5, layout.Decks[0].ElapsedSeconds);
    }

    [Fact]
    public void AnIndexItDoesNotWatchIsIgnored()
    {
        var snapshot = Snapshot(TwoDeckHorizontal);
        var live = new LiveLayout(snapshot);
        var title = IndexOf(snapshot, snapshot.Elements.First(element => element.Value == "Breaks 1").Frame);

        Assert.Equal(Layout(TwoDeckHorizontal), live.Refresh(new Dictionary<int, string> { [title] = "Another" }));
    }

    [Fact]
    public void TheLiveDecksMirrorShowsBothDecksOfATwoDeckLayout()
    {
        var decks = LiveDecksSnapshot.Of(Layout(TwoDeckHorizontal));

        Assert.Equal([new LiveDeckSnapshot(1, "Breaks 1", false, null), new LiveDeckSnapshot(2, "House 3", false, null)], decks.Decks);
        Assert.Equal(0, decks.FramesPerSecond);
        Assert.Equal(0, decks.LatencyMilliseconds);
    }

    [Fact]
    public void TheLiveDecksMirrorOfAFourDeckLayoutShowsOnlyTheLoadedDecks()
    {
        var layout = Layout(FourDeckHorizontal);

        var decks = LiveDecksSnapshot.Of(layout);

        var loaded = layout.Decks.Where(deck => deck.BPM is not null || deck.Title is not null).Select(deck => deck.Number).ToList();
        Assert.Equal(loaded, decks.Decks.Select(deck => deck.Number));
        Assert.True(loaded.Count < 4);
    }

    [Fact]
    public void TheLiveDecksMirrorReadsLiveOnceRekordboxIsActive()
    {
        var context = new StatusContext(OverlayActivation.Active, new OverlaySettings(), "7.2.19", layoutName: "2Deck Horizontal");

        Assert.Equal(new StatusLine(StatusTone.Live, "Live"), StatusText.LiveDecks(context, LiveDecksSnapshot.Of(Layout(TwoDeckHorizontal))));
    }

    [Theory]
    [InlineData("7.2.19.0342", "7.2.19")]
    [InlineData("7.2.19", "7.2.19")]
    [InlineData(" 7.2 ", "7.2")]
    [InlineData("7.2.19.0342 (beta)", "7.2.19")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TheRekordboxVersionKeepsItsFirstThreeNumbers(string? productVersion, string? version) =>
        Assert.Equal(version, RekordboxProduct.ShortVersion(productVersion));

    [Fact]
    public void AReadWhileAPopupMenuIsOpenIsTransient()
    {
        // While one of rekordbox's popup menus is open, its window shows UI Automation only its title bar.
        Assert.True(LiveLayout.IsTransientRead(Layout("uia-7.2.19-perf-menu-open.json")));
    }

    [Theory]
    [InlineData(TwoDeckHorizontal)]
    [InlineData("uia-7.2.19-perf-browse.json")]
    [InlineData("uia-7.2.19-export.json")]
    public void AReadThatFindsTheModeIsNotTransient(string fixture) => Assert.False(LiveLayout.IsTransientRead(Layout(fixture)));

    [Fact]
    public void AnUnknownModeWithItsLayoutBoxIsNotTransient()
    {
        var snapshot = Snapshot(TwoDeckHorizontal).Changing(element => element.HelpText?.StartsWith("Change EXPORT/PERFORMANCE/EDIT mode") == true,
            element => element with { Value = "LIGHTING" });

        var layout = RekordboxLayout.Locate(snapshot);

        Assert.Equal(RekordboxMode.Unknown, layout.Mode);
        Assert.False(LiveLayout.IsTransientRead(layout));
    }

    [Theory]
    [InlineData(40, 1000)]
    [InlineData(100, 1000)]
    [InlineData(180, 1800)]
    [InlineData(250, 2500)]
    [InlineData(900, 3000)]
    public void TheWholeWindowIsReadAgainAtMostATenthOfTheTime(int readMilliseconds, int intervalMilliseconds) =>
        Assert.Equal(TimeSpan.FromMilliseconds(intervalMilliseconds), LiveLayout.FullReadInterval(TimeSpan.FromMilliseconds(readMilliseconds)));
}
