using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class WindowPlacementTests
{
    static readonly DisplayMonitor Laptop = new(new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1032), 144);
    static readonly DisplayMonitor External = new(new ScreenRect(1920, -200, 2560, 1440), new ScreenRect(1920, -200, 2560, 1400), 96);
    static readonly WindowSize Minimum = MainWindowMetrics.MinimumSize;
    static readonly WindowSize Default = MainWindowMetrics.DefaultSize;

    [Fact]
    public void SetupKey_DescribesEveryMonitorInOrderWhateverTheEnumerationOrder()
    {
        Assert.Equal("0,0,1920x1080@144 | 1920,-200,2560x1440@96", WindowPlacement.SetupKey([External, Laptop]));
        Assert.Equal(WindowPlacement.SetupKey([Laptop, External]), WindowPlacement.SetupKey([External, Laptop]));
        Assert.NotEqual(WindowPlacement.SetupKey([Laptop]), WindowPlacement.SetupKey([Laptop, External]));
    }

    [Fact]
    public void SetupKey_ChangesWithTheScaling()
    {
        Assert.NotEqual(WindowPlacement.SetupKey([Laptop]), WindowPlacement.SetupKey([Laptop with { Dpi = 120 }]));
    }

    [Theory]
    [InlineData(false, "100 -40 1560 1050")]
    [InlineData(true, "100 -40 1560 1050 maximized")]
    public void StoredValue_RoundTrips(bool isMaximized, string stored)
    {
        var placement = new WindowPlacement(new ScreenRect(100, -40, 1560, 1050), isMaximized);
        Assert.Equal(stored, placement.StoredValue);
        Assert.Equal(placement, WindowPlacement.FromStoredValue(stored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1 2 3")]
    [InlineData("1 2 3 4 minimized")]
    [InlineData("1 2 0 4")]
    [InlineData("1 2 3 -4")]
    [InlineData("a 2 3 4")]
    [InlineData("1 2 NaN 4")]
    [InlineData("1 2 3 4 maximized extra")]
    public void FromStoredValue_RejectsAnythingElse(string? stored)
    {
        Assert.Null(WindowPlacement.FromStoredValue(stored));
    }

    [Fact]
    public void Fit_KeepsAFrameThatAlreadyFits()
    {
        var frame = new ScreenRect(2000, 0, 1040, 700);
        Assert.Equal(frame, WindowPlacement.Fit(frame, External.WorkArea, new ScreenSize(940, 640)));
    }

    [Fact]
    public void Fit_MovesAFrameHangingOffTheWorkAreaBackOnIt()
    {
        var fitted = WindowPlacement.Fit(new ScreenRect(1500, 900, 1040, 700), Laptop.WorkArea, new ScreenSize(940, 640));
        Assert.Equal(new ScreenRect(880, 332, 1040, 700), fitted);
    }

    [Fact]
    public void Fit_ShrinksAFrameLargerThanTheWorkAreaButNotBelowTheMinimum()
    {
        var small = new ScreenRect(0, 0, 1366, 728);
        var fitted = WindowPlacement.Fit(new ScreenRect(-50, -50, 1560, 1050), small, new ScreenSize(1175, 800));
        Assert.Equal(new ScreenRect(0, 0, 1366, 800), fitted);
    }

    [Fact]
    public void MonitorFor_PicksTheMonitorHoldingMostOfTheFrame()
    {
        var frame = new ScreenRect(1700, 100, 1040, 700);
        Assert.Equal(External, WindowPlacement.MonitorFor(frame, [Laptop, External]));
        Assert.Equal(Laptop, WindowPlacement.MonitorFor(frame with { X = 1000 }, [Laptop, External]));
    }

    [Fact]
    public void MonitorFor_PicksTheNearestMonitorForAFrameOffEveryScreen()
    {
        Assert.Equal(External, WindowPlacement.MonitorFor(new ScreenRect(6000, 0, 1040, 700), [Laptop, External]));
        Assert.Equal(Laptop, WindowPlacement.MonitorFor(new ScreenRect(-3000, 300, 1040, 700), [Laptop, External]));
        Assert.Null(WindowPlacement.MonitorFor(new ScreenRect(0, 0, 10, 10), []));
    }

    [Fact]
    public void Restored_WithoutAStoredFrameCentresTheDefaultSizeOnTheMainMonitorAtItsScale()
    {
        var placement = WindowPlacement.Restored(null, [Laptop, External], Laptop, Default, Minimum);
        Assert.Equal(new WindowPlacement(new ScreenRect(180, 0, 1560, 1032), false), placement);
        var external = WindowPlacement.Restored(null, [Laptop, External], External, Default, Minimum);
        Assert.Equal(new WindowPlacement(new ScreenRect(2680, 150, 1040, 700), false), external);
    }

    [Fact]
    public void Restored_FitsTheStoredFrameToItsMonitorAndKeepsMaximized()
    {
        var stored = new WindowPlacement(new ScreenRect(4000, 1000, 1200, 800), true);
        var placement = WindowPlacement.Restored(stored, [Laptop, External], Laptop, Default, Minimum);
        Assert.Equal(new WindowPlacement(new ScreenRect(3280, 400, 1200, 800), true), placement);
    }
}
