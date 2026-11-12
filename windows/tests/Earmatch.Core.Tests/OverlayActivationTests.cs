using Earmatch.Core;
using static Earmatch.Core.Tests.Fixture;

namespace Earmatch.Core.Tests;

public class OverlayActivationTests
{
    static readonly RekordboxLayout Active = Layout("uia-7.2.19-perf-2deck-horizontal.json");

    [Fact]
    public void ActiveOnlyWhenEverythingLinesUp()
    {
        Assert.Equal(OverlayActivation.Active, OverlayActivation.Evaluate(isRunning: true, layout: Active, isFrontmost: true));
    }

    [Fact]
    public void WaitsForRekordboxAndItsWindow()
    {
        Assert.Equal(OverlayActivation.RekordboxNotRunning, OverlayActivation.Evaluate(isRunning: false, layout: null, isFrontmost: false));
        Assert.Equal(OverlayActivation.MainWindowNotFound, OverlayActivation.Evaluate(isRunning: true, layout: null, isFrontmost: true));
    }

    [Fact]
    public void PausesWhenRekordboxIsNotInFront()
    {
        Assert.Equal(OverlayActivation.RekordboxNotFrontmost, OverlayActivation.Evaluate(isRunning: true, layout: Active, isFrontmost: false));
    }

    [Fact]
    public void AsksForPerformanceModeAndASupportedLayout()
    {
        var export = Layout("uia-7.2.19-export.json");
        Assert.Equal(OverlayActivation.WrongMode(RekordboxMode.Export), OverlayActivation.Evaluate(isRunning: true, layout: export, isFrontmost: true));

        var browse = Layout("uia-7.2.19-perf-browse.json");
        Assert.Equal(OverlayActivation.WrongLayout("Browse"), OverlayActivation.Evaluate(isRunning: true, layout: browse, isFrontmost: true));
    }

    [Fact]
    public void WaitsWhenTheWaveformCannotBeFound()
    {
        var snapshot = Snapshot("uia-7.2.19-perf-2deck-horizontal.json").Removing(element => element.Name == "RST");

        var layout = RekordboxLayout.Locate(snapshot);

        Assert.Equal(OverlayActivation.DecksNotFound, OverlayActivation.Evaluate(isRunning: true, layout: layout, isFrontmost: true));
    }

    [Theory]
    [InlineData("uia-7.2.19-perf-2deck-horizontal.json")]
    [InlineData("uia-7.2.19-perf-2deck-vertical.json")]
    [InlineData("uia-7.2.19-perf-4deck-horizontal.json")]
    [InlineData("uia-7.2.19-perf-4deck-vertical.json")]
    public void EverySupportedLayoutActivates(string name)
    {
        Assert.Equal(OverlayActivation.Active, OverlayActivation.Evaluate(isRunning: true, layout: Layout(name), isFrontmost: true));
    }

    [Fact]
    public void AnUnknownLayoutAsksForASupportedOne()
    {
        var snapshot = Snapshot("uia-7.2.19-perf-4deck-horizontal.json")
            .Changing(element => element.Value == "4Deck Horizontal", element => element with { Value = "6Deck Horizontal" });

        Assert.Equal(OverlayActivation.WrongLayout("6Deck Horizontal"),
            OverlayActivation.Evaluate(isRunning: true, layout: RekordboxLayout.Locate(snapshot), isFrontmost: true));
    }

    [Fact]
    public void EveryStateExplainsItselfToTheUser()
    {
        Assert.Equal("Waiting for rekordbox 7", OverlayActivation.RekordboxNotRunning.Message);
        Assert.Equal("Waiting for the rekordbox window", OverlayActivation.MainWindowNotFound.Message);
        Assert.Equal("Paused until rekordbox is in front", OverlayActivation.RekordboxNotFrontmost.Message);
        Assert.Equal("Switch rekordbox to PERFORMANCE mode", OverlayActivation.WrongMode(RekordboxMode.Export).Message);
        Assert.Equal("Browse view is open; switch back to a 2Deck or 4Deck layout", OverlayActivation.WrongLayout("Browse").Message);
        Assert.Equal("Switch the rekordbox layout to a 2Deck or 4Deck layout", OverlayActivation.WrongLayout(null).Message);
        Assert.Equal("Waiting for the decks and the waveform", OverlayActivation.DecksNotFound.Message);
        Assert.Equal("Active on rekordbox", OverlayActivation.Active.Message);
    }

    [Fact]
    public void OnlyTheActiveStateIsActive()
    {
        Assert.True(OverlayActivation.Active.IsActive);
        Assert.False(OverlayActivation.DecksNotFound.IsActive);
        Assert.False(OverlayActivation.WrongLayout("Browse").IsActive);
        Assert.NotEqual(OverlayActivation.WrongLayout("Browse"), OverlayActivation.WrongLayout(null));
    }
}
