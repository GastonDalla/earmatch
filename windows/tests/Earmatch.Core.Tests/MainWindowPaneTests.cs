using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class MainWindowPaneTests
{
    [Fact]
    public void TheSidebarListsLiveOverlayPracticeAndSetupInThatOrder()
    {
        Assert.Equal([MainWindowPane.Live, MainWindowPane.Overlay, MainWindowPane.Practice, MainWindowPane.Setup], MainWindowPane.AllCases);
    }

    [Fact]
    public void ControlOneToFourSelectsThePanesInSidebarOrder()
    {
        Assert.Equal([1, 2, 3, 4], MainWindowPane.AllCases.Select(pane => pane.ShortcutNumber));
        Assert.Equal([MainWindowPane.Live, MainWindowPane.Overlay, MainWindowPane.Practice, MainWindowPane.Setup],
            new[] { "1", "2", "3", "4" }.Select(MainWindowPane.FromKeyEquivalent));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("5")]
    [InlineData("9")]
    [InlineData("a")]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("Ctrl+1")]
    public void OtherKeysSelectNoPane(string key)
    {
        Assert.Null(MainWindowPane.FromKeyEquivalent(key));
    }

    [Fact]
    public void TheArrowKeysMoveOnePaneAndStopAtTheEnds()
    {
        Assert.Equal(MainWindowPane.Overlay, MainWindowPane.Live.Moved(1));
        Assert.Equal(MainWindowPane.Overlay, MainWindowPane.Practice.Moved(-1));
        Assert.Equal(MainWindowPane.Live, MainWindowPane.Live.Moved(-1));
        Assert.Equal(MainWindowPane.Setup, MainWindowPane.Setup.Moved(1));
    }

    [Fact]
    public void TheLastPaneIsRemembered()
    {
        Assert.Equal("mainWindowPane", MainWindowPane.StorageKey);
        foreach (var pane in MainWindowPane.AllCases)
        {
            Assert.Equal(pane, MainWindowPane.Initial(pane.RawValue));
        }
        Assert.Equal(["live", "overlay", "practice", "setup"], MainWindowPane.AllCases.Select(pane => pane.RawValue));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Live")]
    [InlineData("browse")]
    [InlineData("4")]
    public void AnUnknownRememberedPaneOpensLive(string? stored)
    {
        Assert.Equal(MainWindowPane.Live, MainWindowPane.Initial(stored));
    }

    [Fact]
    public void EveryPaneTitleIsLocalized()
    {
        var titles = Localized.Marked(() => MainWindowPane.AllCases.Select(pane => pane.Title).ToList());
        Assert.Equal(["⟦Live⟧", "⟦Overlay⟧", "⟦Practice⟧", "⟦Setup⟧"], titles);
    }
}

public class MainWindowStatusTests
{
    [Fact]
    public void TurnedOffEarmatchReadsOff()
    {
        Assert.Equal(MainWindowStatus.Off, MainWindowStatus.From(isEnabled: false, OverlayActivation.Active));
        Assert.Equal(MainWindowStatus.Off, MainWindowStatus.From(isEnabled: false, OverlayActivation.RekordboxNotRunning));
    }

    [Fact]
    public void AnActiveOverlayReadsLive()
    {
        Assert.Equal(MainWindowStatus.Live, MainWindowStatus.From(isEnabled: true, OverlayActivation.Active));
    }

    public static readonly TheoryData<OverlayActivation> Inactive = new(
        OverlayActivation.RekordboxNotRunning, OverlayActivation.MainWindowNotFound, OverlayActivation.RekordboxNotFrontmost,
        OverlayActivation.WrongMode(RekordboxMode.Export), OverlayActivation.WrongLayout("Browse"), OverlayActivation.DecksNotFound);

    [Theory]
    [MemberData(nameof(Inactive))]
    public void AnEnabledButInactiveOverlayReadsWaiting(OverlayActivation activation)
    {
        Assert.Equal(MainWindowStatus.Waiting, MainWindowStatus.From(isEnabled: true, activation));
    }

    [Fact]
    public void EachStatusUsesTheMatchingTone()
    {
        Assert.Equal(StatusTone.Live, MainWindowStatus.Live.Tone);
        Assert.Equal(StatusTone.Off, MainWindowStatus.Off.Tone);
        Assert.Equal(StatusTone.Waiting, MainWindowStatus.Waiting.Tone);
    }

    [Fact]
    public void EveryStatusTitleIsLocalized()
    {
        var statuses = new[] { MainWindowStatus.Live, MainWindowStatus.Off, MainWindowStatus.Waiting };
        var titles = Localized.Marked(() => statuses.Select(status => status.Title).ToList());
        Assert.All(titles, title => Assert.True(Localized.IsLocalized(title), title));
        Assert.Equal(statuses.Length, titles.Distinct().Count());
        Assert.Equal(["Live on rekordbox", "Earmatch is off", "Waiting for rekordbox"], statuses.Select(status => L10n.English(() => status.Title)));
    }
}
