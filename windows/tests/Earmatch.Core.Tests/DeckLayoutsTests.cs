using Earmatch.Core;
using static Earmatch.Core.Tests.Fixture;

namespace Earmatch.Core.Tests;

public class DeckLayoutsTests
{
    [Fact]
    public void EachLayoutNameMapsToItsDecksAndStrips()
    {
        Assert.Equal(["2Deck Horizontal", "2Deck Vertical", "4Deck Horizontal", "4Deck Vertical"], DeckArrangement.AllCases.Select(a => a.RawValue));
        Assert.Equal([2, 2, 4, 4], DeckArrangement.AllCases.Select(a => a.DeckCount));
        Assert.Equal([StripOrientation.Horizontal, StripOrientation.Vertical, StripOrientation.Horizontal, StripOrientation.Vertical],
            DeckArrangement.AllCases.Select(a => a.Orientation));
        Assert.Equal([3, 1, 2, 4], DeckArrangement.FourDeckVertical.StripDecks);
        Assert.Equal([1, 2, 3, 4], DeckArrangement.FourDeckHorizontal.StripDecks);
        Assert.Equal(DeckArrangement.TwoDeckVertical, DeckArrangement.FromRawValue("2Deck Vertical"));
        Assert.Null(DeckArrangement.FromRawValue("Browse"));
        Assert.Null(DeckArrangement.FromRawValue(null));
    }

    [Fact]
    public void TwoDeckVerticalHasTwoColumnsUnderTheButtonBar()
    {
        var layout = Layout("uia-7.2.19-perf-2deck-vertical.json");

        Assert.Equal(RekordboxMode.Performance, layout.Mode);
        Assert.Equal(DeckArrangement.TwoDeckVertical, layout.Arrangement);
        Assert.True(layout.IsSupportedPerformanceLayout);
        Assert.Equal(Rect(776, 146, 496, 566), layout.StackedWaveform);
        Assert.Equal([1, 2], layout.Decks.Select(deck => deck.Number));
        var deck1 = layout.Decks[0];
        var deck2 = layout.Decks[1];
        Assert.Equal(Rect(776, 146, 248, 566), deck1.WaveformStrip);
        Assert.Equal(Rect(1024, 146, 248, 566), deck2.WaveformStrip);
        Assert.Equal(StripOrientation.Vertical, deck1.Strip?.Orientation);
        Assert.Equal(Rect(776, 202, 248, 510), deck1.Strip?.MarkerArea);
        Assert.Equal(Rect(1024, 202, 248, 510), deck2.Strip?.MarkerArea);
        Assert.Equal(429, deck1.Strip?.Center);
        Assert.Null(deck1.BarsText);
        Assert.Equal("1.1Bars", deck2.BarsText);
        Assert.Equal(Rect(614, 210, 140, 26), deck1.MasterButtonFrame);
        Assert.Equal(Rect(1892, 210, 140, 26), deck2.MasterButtonFrame);
        Assert.Equal(Rect(260, 206, 80, 24), deck1.HeaderBPMFrame);
        Assert.Equal(Rect(1538, 206, 80, 24), deck2.HeaderBPMFrame);
        Assert.Equal(Rect(466, 492, 140, 52), deck1.JogBPMFrame);
        Assert.Equal(Rect(1458, 492, 140, 52), deck2.JogBPMFrame);
        Assert.Equal(Rect(426, 548, 116, 30), deck1.PitchFrame);
        Assert.Equal(Rect(1418, 548, 116, 30), deck2.PitchFrame);
        Assert.Equal(Rect(4, 250, 762, 72), deck1.OverviewFrame);
        Assert.Equal(Rect(1282, 250, 762, 72), deck2.OverviewFrame);
        Assert.True(deck1.BPM == 130 && deck2.BPM == 140);
        Assert.True(deck1.Title == "Breaks 1" && deck2.Title == "House 3");
        Assert.Equal(0, deck1.ElapsedSeconds);
        Assert.Equal(7.3, deck1.RemainingSeconds);
        Assert.Equal(13.7, deck2.RemainingSeconds);
    }

    [Fact]
    public void FourDeckHorizontalStacksFourStripsAndNumbersTheQuadrants()
    {
        var layout = Layout("uia-7.2.19-perf-4deck-horizontal.json");

        Assert.Equal(DeckArrangement.FourDeckHorizontal, layout.Arrangement);
        Assert.True(layout.IsSupportedPerformanceLayout);
        Assert.Equal(Rect(0, 146, 2048, 388), layout.StackedWaveform);
        Assert.Equal([1, 2, 3, 4], layout.Decks.Select(deck => deck.Number));
        Assert.Equal(new double[] { 146, 242, 340, 436 }.Select(y => (ScreenRect?)Rect(24, y, 2000, 96)), layout.Decks.Select(deck => deck.WaveformStrip));
        Assert.All(layout.Decks, deck => Assert.True(deck.Strip?.Orientation == StripOrientation.Horizontal && deck.Strip?.Center == 1024));
        Assert.All(layout.Decks, deck => Assert.Equal(deck.WaveformStrip, deck.Strip?.MarkerArea));
        Assert.Equal(
            [Rect(866, 596, 140, 26), Rect(1892, 596, 140, 26), Rect(866, 978, 140, 26), Rect(1892, 978, 140, 26)],
            layout.Decks.Select(deck => deck.MasterButtonFrame));
        var deck1 = layout.Decks[0];
        var deck2 = layout.Decks[1];
        Assert.True(deck1.Title == "Breaks 1" && deck1.BPM == 130);
        Assert.True(deck2.Title == "House 3" && deck2.BPM == 140);
        Assert.Equal(Rect(316, 592, 88, 24), deck1.HeaderBPMFrame);
        Assert.Equal(Rect(1342, 592, 88, 24), deck2.HeaderBPMFrame);
        Assert.Equal(Rect(772, 768, 108, 46), deck1.JogBPMFrame);
        Assert.Equal(Rect(1192, 768, 108, 46), deck2.JogBPMFrame);
        Assert.Equal(Rect(752, 810, 70, 22), deck1.PitchFrame);
        Assert.Equal(Rect(1172, 810, 70, 22), deck2.PitchFrame);
        Assert.Equal(0, deck1.ElapsedSeconds);
        Assert.Equal(7.3, deck1.RemainingSeconds);
        Assert.Equal(13.7, deck2.RemainingSeconds);
        Assert.Equal(
            [Rect(4, 628, 1014, 52), Rect(1030, 628, 1014, 52), Rect(4, 1010, 1014, 52), Rect(1030, 1010, 1014, 52)],
            layout.Decks.Select(deck => deck.OverviewFrame));
    }

    [Theory]
    [InlineData("uia-7.2.19-perf-4deck-horizontal.json")]
    [InlineData("uia-7.2.19-perf-4deck-vertical.json")]
    public void EmptyDecksInFourDeckLayoutsHaveNothingToCover(string name)
    {
        var empty = Layout(name).Decks.Where(deck => deck.Number >= 3).ToList();

        Assert.Equal(2, empty.Count);
        Assert.All(empty, deck =>
        {
            Assert.Null(deck.Title);
            Assert.Null(deck.BPM);
            Assert.Null(deck.HeaderBPMFrame);
            Assert.Null(deck.JogBPMFrame);
            Assert.Null(deck.PitchFrame);
            Assert.Equal(0, deck.ElapsedSeconds);
            Assert.Equal(0, deck.RemainingSeconds);
        });
    }

    [Fact]
    public void FourDeckVerticalOrdersItsColumnsThreeOneTwoFour()
    {
        var layout = Layout("uia-7.2.19-perf-4deck-vertical.json");

        Assert.Equal(DeckArrangement.FourDeckVertical, layout.Arrangement);
        Assert.True(layout.IsSupportedPerformanceLayout);
        Assert.Equal(Rect(776, 182, 496, 766), layout.StackedWaveform);
        Assert.Equal([1, 2, 3, 4], layout.Decks.Select(deck => deck.Number));
        var strips = layout.Decks.ToDictionary(deck => deck.Number, deck => deck.WaveformStrip);
        Assert.Equal(Rect(776, 182, 124, 766), strips[3]);
        Assert.Equal(Rect(900, 182, 124, 766), strips[1]);
        Assert.Equal(Rect(1024, 182, 124, 766), strips[2]);
        Assert.Equal(Rect(1148, 182, 124, 766), strips[4]);
        Assert.All(layout.Decks, deck => Assert.True(deck.Strip?.Orientation == StripOrientation.Vertical && deck.Strip?.Center == 565));
        Assert.Equal(Rect(900, 202, 124, 746), layout.Decks.Single(deck => deck.Number == 1).Strip?.MarkerArea);
        Assert.Equal([null, "1.1Bars", null, null], layout.Decks.Select(deck => deck.BarsText));
        Assert.Equal(
            [new ScreenPoint(614, 210), new ScreenPoint(1892, 210), new ScreenPoint(614, 628), new ScreenPoint(1892, 628)],
            layout.Decks.Select(deck => deck.MasterButtonFrame.Origin));
        var deck1 = layout.Decks[0];
        var deck2 = layout.Decks[1];
        Assert.Equal(Rect(288, 206, 88, 24), deck1.HeaderBPMFrame);
        Assert.Equal(Rect(1566, 206, 88, 24), deck2.HeaderBPMFrame);
        Assert.Equal(Rect(520, 410, 108, 46), deck1.JogBPMFrame);
        Assert.Equal(Rect(1444, 410, 108, 46), deck2.JogBPMFrame);
        Assert.Equal(Rect(500, 452, 70, 22), deck1.PitchFrame);
        Assert.Equal(Rect(1424, 452, 70, 22), deck2.PitchFrame);
        Assert.True(deck1.Title == "Breaks 1" && deck2.Title == "House 3");
        Assert.Equal(7.3, deck1.RemainingSeconds);
        Assert.Equal(13.7, deck2.RemainingSeconds);
        Assert.Equal(
            [Rect(4, 250, 762, 72), Rect(1282, 250, 762, 72), Rect(4, 668, 762, 72), Rect(1282, 668, 762, 72)],
            layout.Decks.Select(deck => deck.OverviewFrame));
    }

    [Fact]
    public void TheLiveHeaderBPMRefreshWorksForEveryDeck()
    {
        var snapshot = Snapshot("uia-7.2.19-perf-4deck-vertical.json");
        var layout = RekordboxLayout.Locate(snapshot);
        var indices = layout.HeaderBPMElementIndices(snapshot);

        Assert.Equal([1, 2], indices.Keys.Order());
        layout.ApplyLiveHeaderBPM(new Dictionary<int, string> { [1] = "136.40", [2] = "124.00" });
        Assert.Equal([136.4, 124, null, null], layout.Decks.Select(deck => deck.BPM));
    }

    [Fact]
    public void ALayoutWithoutItsWaveformAnchorWaitsForTheWaveform()
    {
        var snapshot = Snapshot("uia-7.2.19-perf-4deck-vertical.json").Removing(element => element.Name == "RST");

        var layout = RekordboxLayout.Locate(snapshot);

        Assert.Null(layout.StackedWaveform);
        Assert.Equal(4, layout.Decks.Count);
        Assert.False(layout.IsSupportedPerformanceLayout);
    }

    [Fact]
    public void AnUnknownLayoutHasNoArrangement()
    {
        var snapshot = Snapshot("uia-7.2.19-perf-4deck-horizontal.json")
            .Changing(element => element.Value == "4Deck Horizontal", element => element with { Value = "6Deck Horizontal" });

        var layout = RekordboxLayout.Locate(snapshot);

        Assert.Equal("6Deck Horizontal", layout.LayoutName);
        Assert.Null(layout.Arrangement);
        Assert.False(layout.IsSupportedPerformanceLayout);
    }

    [Fact]
    public void TheLocatorFollowsTheDisplayScale()
    {
        var full = Snapshot("uia-7.2.19-perf-2deck-vertical.json");
        static ScreenRect Half(ScreenRect rect) => new(rect.X / 2, rect.Y / 2, rect.Width / 2, rect.Height / 2);
        var half = full with
        {
            WindowFrame = Half(full.WindowFrame),
            Elements = full.Elements.Select(element => element with { Frame = Half(element.Frame) }).ToList(),
            Scale = 1,
        };

        var layout = RekordboxLayout.Locate(half);

        Assert.True(layout.IsSupportedPerformanceLayout);
        Assert.Equal(Rect(388, 73, 248, 283), layout.StackedWaveform);
        Assert.Equal([Rect(388, 73, 124, 283), Rect(512, 73, 124, 283)], layout.Decks.Select(deck => deck.WaveformStrip));
        Assert.Equal(Rect(388, 101, 124, 255), layout.Decks[0].Strip?.MarkerArea);
        Assert.Equal(Rect(213, 274, 58, 15), layout.Decks[0].PitchFrame);
        Assert.Equal(Rect(233, 246, 70, 26), layout.Decks[0].JogBPMFrame);
        Assert.Equal(Rect(130, 103, 40, 12), layout.Decks[0].HeaderBPMFrame);
        Assert.Equal("1.1Bars", layout.Decks[1].BarsText);
        Assert.Equal(7.3, layout.Decks[0].RemainingSeconds);
    }
}
