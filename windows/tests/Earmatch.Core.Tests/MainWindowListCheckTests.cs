using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class MainWindowListCheckTests
{
    [Fact]
    public void AListWithOtherWindowsButNoMainWindowMeansItWasClosed()
    {
        var check = new MainWindowListCheck();
        Assert.True(check.IsMainWindowGone(windowTitles: ["Preferences"]));
    }

    [Fact]
    public void AListWithTheMainWindowMeansItIsOpen()
    {
        var check = new MainWindowListCheck();
        Assert.False(check.IsMainWindowGone(windowTitles: ["Preferences", "rekordbox"]));
    }

    [Fact]
    public void AnEmptyListIsTransientUntilASecondReadConfirmsIt()
    {
        var check = new MainWindowListCheck();
        var first = check.IsMainWindowGone(windowTitles: []);
        var second = check.IsMainWindowGone(windowTitles: []);

        Assert.False(first);
        Assert.True(second);
    }

    [Fact]
    public void AFailedReadIsUnknownAndBreaksTheConfirmation()
    {
        var check = new MainWindowListCheck();
        var failed = check.IsMainWindowGone(windowTitles: null);
        _ = check.IsMainWindowGone(windowTitles: []);
        var afterFailure = check.IsMainWindowGone(windowTitles: null);
        var emptyAgain = check.IsMainWindowGone(windowTitles: []);

        Assert.False(failed);
        Assert.False(afterFailure);
        Assert.False(emptyAgain);
    }

    [Fact]
    public void FindingTheWindowForgetsAnEarlierEmptyList()
    {
        var check = new MainWindowListCheck();
        _ = check.IsMainWindowGone(windowTitles: []);
        check.MainWindowFound();

        Assert.False(check.IsMainWindowGone(windowTitles: []));
    }
}
