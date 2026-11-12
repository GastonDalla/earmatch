using Earmatch.Core;
using static Earmatch.Core.Tests.Fixture;

namespace Earmatch.Core.Tests;

public class RekordboxLayoutTests
{
    const string TwoDeckHorizontal = "uia-7.2.19-perf-2deck-horizontal.json";

    [Fact]
    public void LocatesBothDecksInTwoDeckHorizontalPerformanceView()
    {
        var layout = Layout(TwoDeckHorizontal);

        Assert.Equal(RekordboxMode.Performance, layout.Mode);
        Assert.Equal("2Deck Horizontal", layout.LayoutName);
        Assert.Equal(DeckArrangement.TwoDeckHorizontal, layout.Arrangement);
        Assert.True(layout.IsSupportedPerformanceLayout);
        Assert.Equal(Rect(0, 146, 2048, 276), layout.StackedWaveform);
        Assert.Equal([1, 2], layout.Decks.Select(deck => deck.Number));

        var deck1 = layout.Decks[0];
        var deck2 = layout.Decks[1];
        Assert.Equal(130.0, deck1.BPM);
        Assert.Equal(140.0, deck2.BPM);
        Assert.Equal(Rect(358, 490, 88, 28), deck1.HeaderBPMFrame);
        Assert.Equal(Rect(1384, 490, 88, 28), deck2.HeaderBPMFrame);
        Assert.Equal(Rect(758, 714, 108, 46), deck1.JogBPMFrame);
        Assert.Equal(Rect(1202, 714, 108, 46), deck2.JogBPMFrame);
        Assert.Equal(Rect(866, 496, 140, 26), deck1.MasterButtonFrame);
        Assert.Equal(Rect(1892, 496, 140, 26), deck2.MasterButtonFrame);
        Assert.Equal(Rect(0, 146, 2048, 138), deck1.WaveformStrip);
        Assert.Equal(Rect(0, 284, 2048, 138), deck2.WaveformStrip);
        Assert.All(layout.Decks, deck => Assert.Equal(StripOrientation.Horizontal, deck.Strip?.Orientation));
        Assert.All(layout.Decks, deck => Assert.Equal(deck.WaveformStrip, deck.Strip?.MarkerArea));
        Assert.All(layout.Decks, deck => Assert.Equal(1024, deck.Strip?.Center));
        Assert.All(layout.Decks, deck => Assert.Null(deck.BarsText));
        Assert.Equal([Rect(4, 540, 1014, 70), Rect(1030, 540, 1014, 70)], layout.Decks.Select(deck => deck.OverviewFrame));
        Assert.Equal("Breaks 1", deck1.Title);
        Assert.Equal("House 3", deck2.Title);
        Assert.Equal(0.0, deck1.ElapsedSeconds);
        Assert.Equal(7.3, deck1.RemainingSeconds);
        Assert.Equal(13.7, deck2.RemainingSeconds);
    }

    [Fact]
    public void AnArtistMadeOfDigitsIsNotMistakenForTheBPM()
    {
        var snapshot = Snapshot(TwoDeckHorizontal)
            .Changing(element => element.ControlType == UiaControlType.Text && element.Frame == Rect(128, 490, 200, 28),
                element => element with { Value = "112" });

        var deck1 = RekordboxLayout.Locate(snapshot).Decks[0];

        Assert.Equal(130.0, deck1.BPM);
        Assert.Equal(Rect(358, 490, 88, 28), deck1.HeaderBPMFrame);
    }

    [Fact]
    public void ExportModeDoesNotActivate()
    {
        var layout = Layout("uia-7.2.19-export.json");

        Assert.Equal(RekordboxMode.Export, layout.Mode);
        Assert.Equal("1 PLAYER", layout.LayoutName);
        Assert.Null(layout.Arrangement);
        Assert.False(layout.IsSupportedPerformanceLayout);
        Assert.Empty(layout.Decks);
        Assert.Null(layout.StackedWaveform);
    }

    [Fact]
    public void TheBrowseViewIsNotASupportedLayout()
    {
        var layout = Layout("uia-7.2.19-perf-browse.json");

        Assert.Equal(RekordboxMode.Performance, layout.Mode);
        Assert.Equal("Browse", layout.LayoutName);
        Assert.Null(layout.Arrangement);
        Assert.Null(layout.StackedWaveform);
        Assert.Equal([1, 2, 3, 4], layout.Decks.Select(deck => deck.Number));
        Assert.False(layout.IsSupportedPerformanceLayout);
    }

    [Fact]
    public void OtherLayoutsDoNotActivate()
    {
        var snapshot = Snapshot(TwoDeckHorizontal).Changing(element => element.Value == "2Deck Horizontal", element => element with { Value = "Browse" });

        var layout = RekordboxLayout.Locate(snapshot);

        Assert.Equal(RekordboxMode.Performance, layout.Mode);
        Assert.Equal("Browse", layout.LayoutName);
        Assert.False(layout.IsSupportedPerformanceLayout);
    }

    [Fact]
    public void AnEmptySnapshotIsUnknownAndInactive()
    {
        var layout = RekordboxLayout.Locate(new RekordboxWindowSnapshot(Rect(0, 0, 1600, 1200), [], Scale: 1));

        Assert.Equal(RekordboxMode.Unknown, layout.Mode);
        Assert.False(layout.IsSupportedPerformanceLayout);
    }

    [Fact]
    public void ALoadedTrackWithoutAnalysedBPMKeepsItsTitle()
    {
        var snapshot = Snapshot(TwoDeckHorizontal)
            .Changing(element => element.Value == "130.00" && element.ControlType == UiaControlType.Text, element => element with { Value = null });

        var deck1 = RekordboxLayout.Locate(snapshot).Decks[0];

        Assert.Null(deck1.BPM);
        Assert.Equal("Breaks 1", deck1.Title);
    }

    [Fact]
    public void ADeckWithABPMButUnreadableTimesKeepsItsTitle()
    {
        var snapshot = Snapshot(TwoDeckHorizontal)
            .Changing(element => element.Value is "-00:07" or "00:00", element => element with { Value = "-123:45" });

        var deck1 = RekordboxLayout.Locate(snapshot).Decks[0];

        Assert.Equal(130.0, deck1.BPM);
        Assert.Equal("Breaks 1", deck1.Title);
        Assert.Null(deck1.ElapsedSeconds);
    }

    [Fact]
    public void LiveHeaderTextsRefreshTheBPMBetweenFullReads()
    {
        var snapshot = Snapshot(TwoDeckHorizontal);
        var layout = RekordboxLayout.Locate(snapshot);
        var indices = layout.HeaderBPMElementIndices(snapshot);

        Assert.Equal([1, 2], indices.Keys.Order());
        Assert.Equal("130.00", snapshot.Elements[indices[1]].Value);
        Assert.Equal("140.00", snapshot.Elements[indices[2]].Value);

        layout.ApplyLiveHeaderBPM(new Dictionary<int, string> { [1] = "136.40", [2] = "" });

        Assert.Equal(136.4, layout.Decks[0].BPM);
        Assert.Null(layout.Decks[1].BPM);
    }

    [Fact]
    public void AMissingLiveTextKeepsTheBPMAndAnEmptyOneClearsIt()
    {
        var layout = Layout(TwoDeckHorizontal);

        layout.ApplyLiveHeaderBPM(new Dictionary<int, string> { [2] = "" });

        Assert.Equal(130.0, layout.Decks[0].BPM);
        Assert.Null(layout.Decks[1].BPM);
    }

    [Fact]
    public void AJogShownAsTextStillHasItsBPMLocated()
    {
        var snapshot = Snapshot(TwoDeckHorizontal)
            .Changing(element => element.HelpText?.StartsWith("Deck BPM display") == true, element => element with { ControlType = UiaControlType.Text });

        var layout = RekordboxLayout.Locate(snapshot);

        Assert.Equal([Rect(758, 714, 108, 46), Rect(1202, 714, 108, 46)], layout.Decks.Select(deck => deck.JogBPMFrame));
    }

    [Fact]
    public void LocatesThePitchPercentageUnderEachJogWithoutTheTempoRange()
    {
        var layout = Layout(TwoDeckHorizontal);

        Assert.Equal(Rect(738, 760, 70, 22), layout.Decks[0].PitchFrame);
        Assert.Equal(Rect(1182, 760, 70, 22), layout.Decks[1].PitchFrame);
    }

    [Fact]
    public void TheTempoRangeStaysOutOfThePitchCoverEvenWithoutItsHelpText()
    {
        var snapshot = Snapshot(TwoDeckHorizontal)
            .Changing(element => element.HelpText?.StartsWith("Tempo Range") == true, element => element with { HelpText = null });

        Assert.Equal(Rect(738, 760, 70, 22), RekordboxLayout.Locate(snapshot).Decks[0].PitchFrame);
    }

    [Fact]
    public void TheWaveformIsFoundFromTheZoomResetButtonBecauseWindowsHasNoCaptureOrMatchingButton()
    {
        var snapshot = Snapshot(TwoDeckHorizontal);
        Assert.DoesNotContain(snapshot.Elements, element => element.Name is "Capture" or "Matching");

        Assert.NotNull(RekordboxLayout.Locate(snapshot).StackedWaveform);
        Assert.Null(RekordboxLayout.Locate(snapshot.Removing(element => element.Name == "RST")).StackedWaveform);
    }

    [Fact]
    public void TheTrafficLightMasterButtonOfTheBrowserIsNotADeck()
    {
        var snapshot = Snapshot("uia-7.2.19-perf-4deck-vertical.json");
        Assert.Contains(snapshot.Elements, element => element.Name == "MASTER" && element.HelpText?.Contains("Traffic Light") == true);

        Assert.Equal(4, RekordboxLayout.Locate(snapshot).Decks.Count);
    }

    [Fact]
    public void ALayoutLocatedTwiceIsEqual()
    {
        Assert.Equal(Layout(TwoDeckHorizontal), Layout(TwoDeckHorizontal));
        Assert.NotEqual(Layout(TwoDeckHorizontal), Layout("uia-7.2.19-perf-2deck-vertical.json"));
    }

    [Fact]
    public void TheDumpKeepsTheWindowFrameAndEveryElementUnderIt()
    {
        var snapshot = Snapshot(TwoDeckHorizontal);

        Assert.Equal(Rect(0, 44, 2048, 1356), snapshot.WindowFrame);
        Assert.Equal(396, snapshot.Elements.Count);
        Assert.Equal(VmDisplayAt200Percent, snapshot.Scale);
        var mode = Assert.Single(snapshot.Elements, element => element.HelpText?.StartsWith("Change EXPORT/PERFORMANCE/EDIT mode") == true);
        Assert.Equal(UiaControlType.ComboBox, mode.ControlType);
        Assert.Equal("PERFORMANCE", mode.Value);
        Assert.Null(mode.Name);
    }
}
