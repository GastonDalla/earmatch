using Earmatch.Core;
using static Earmatch.Core.Tests.Fixture;

namespace Earmatch.Core.Tests;

/// The macOS plans on the Windows fixtures (2 physical pixels per point): the meter band is the macOS 380 x 34 points
/// band in pixels, and the dividers are 2 points (4 pixels) wide.
public class WaveformOverlayPlanTests
{
    static readonly ScreenSize Band = new(760, 68);
    const string TwoDeckHorizontal = "uia-7.2.19-perf-2deck-horizontal.json";
    const string FourDeckHorizontal = "uia-7.2.19-perf-4deck-horizontal.json";
    const string TwoDeckVertical = "uia-7.2.19-perf-2deck-vertical.json";
    const string FourDeckVertical = "uia-7.2.19-perf-4deck-vertical.json";

    static WaveformOverlayPlan Plan(string fixture, WaveformMode mode, bool meters = true)
    {
        var plan = WaveformOverlayPlan.Create(Layout(fixture), mode, meters, Band);
        Assert.NotNull(plan);
        return plan;
    }

    [Fact]
    public void TheLayoutKnowsItsDisplayScale()
    {
        Assert.Equal(2, Layout(TwoDeckHorizontal).Scale);
    }

    [Fact]
    public void TwoDeckHorizontalSplitKeepsItsTwoHalves()
    {
        var plan = Plan(TwoDeckHorizontal, WaveformMode.Split);

        Assert.Equal(Rect(0, 146, 2048, 276), plan.Panel);
        Assert.Equal([1, 2], plan.Panes.Select(pane => pane.Deck));
        Assert.Equal([Rect(0, 0, 1022, 276), Rect(1026, 0, 1022, 276)], plan.Panes.Select(pane => pane.Frame));
        Assert.Equal([138.0, 138.0], plan.Panes.Select(pane => pane.WaveformHeight));
        Assert.Equal([Rect(513, 146, 1022, 138), Rect(513, 284, 1022, 138)], plan.Panes.Select(pane => pane.Source));
        Assert.All(plan.Panes, pane => Assert.True(pane.ShowsMeter));
        Assert.Equal([Rect(1022, 0, 4, 276)], plan.Dividers);
        Assert.True(plan.AllowsMasterWaveform);
        Assert.Empty(plan.SideMeters);
    }

    [Fact]
    public void TwoDeckHorizontalNormalPutsABandOnTopOfEachStrip()
    {
        var plan = Plan(TwoDeckHorizontal, WaveformMode.Normal);

        Assert.Equal(Rect(0, 146, 2048, 276), plan.Panel);
        Assert.Empty(plan.Panes);
        Assert.Equal(new Dictionary<int, ScreenRect> { [1] = Rect(644, 0, 760, 68), [2] = Rect(644, 138, 760, 68) }, plan.Bands);
    }

    [Fact]
    public void NormalWithoutMetersCoversNothing()
    {
        Assert.Null(WaveformOverlayPlan.Create(Layout(TwoDeckHorizontal), WaveformMode.Normal, showsMeters: false, Band));
    }

    [Fact]
    public void FourDeckHorizontalSplitsIntoAQuadrantPerDeck()
    {
        var plan = Plan(FourDeckHorizontal, WaveformMode.Split);

        Assert.Equal(Rect(0, 146, 2048, 388), plan.Panel);
        Assert.Equal([1, 2, 3, 4], plan.Panes.Select(pane => pane.Deck));
        Assert.Equal(
            [Rect(0, 0, 1022, 192), Rect(1026, 0, 1022, 192), Rect(0, 196, 1022, 192), Rect(1026, 196, 1022, 192)],
            plan.Panes.Select(pane => pane.Frame));
        Assert.Equal([96.0, 96.0, 96.0, 96.0], plan.Panes.Select(pane => pane.WaveformHeight));
        Assert.Equal([146.0, 242.0, 340.0, 436.0], plan.Panes.Select(pane => pane.Source.MinY));
        Assert.All(plan.Panes, pane => Assert.True(pane.Source.MidX == 1024 && pane.Source.Width == 1022));
        Assert.Equal([Rect(1022, 0, 4, 388), Rect(0, 192, 2048, 4)], plan.Dividers);
    }

    [Fact]
    public void FourDeckHorizontalBandsLeaveMostOfEachStripVisible()
    {
        var plan = Plan(FourDeckHorizontal, WaveformMode.Normal);

        Assert.Equal([1, 2, 3, 4], plan.Bands.Keys.Order());
        Assert.Equal(Rect(644, 0, 760, 57.6), plan.Bands[1]);
        Assert.Equal(Rect(644, 290, 760, 57.6), plan.Bands[4]);
    }

    [Theory]
    [InlineData(TwoDeckVertical)]
    [InlineData(FourDeckVertical)]
    public void VerticalSplitLeavesTheColumnsAsTheyAre(string fixture)
    {
        var plan = Plan(fixture, WaveformMode.Split);

        Assert.Null(plan.Panel);
        Assert.True(plan.Panes.Count == 0 && plan.Bands.Count == 0 && plan.Dividers.Count == 0);
        Assert.False(plan.AllowsMasterWaveform);
    }

    [Fact]
    public void VerticalHideBothBlacksOutEveryColumn()
    {
        var plan = Plan(FourDeckVertical, WaveformMode.HideBoth);

        Assert.Equal(Rect(776, 182, 496, 766), plan.Panel);
        Assert.Equal([1, 2, 3, 4], plan.Panes.Select(pane => pane.Deck));
        Assert.Equal(
            [Rect(124, 0, 124, 766), Rect(248, 0, 124, 766), Rect(0, 0, 124, 766), Rect(372, 0, 124, 766)],
            plan.Panes.Select(pane => pane.Frame));
        Assert.All(plan.Panes, pane => Assert.True(!pane.ShowsMeter && pane.WaveformHeight == 0));
    }

    [Fact]
    public void VerticalMetersSitOnEachDecksOverviewNextToTheColumns()
    {
        var plan = Plan(FourDeckVertical, WaveformMode.Normal);

        Assert.Equal(new Dictionary<int, ScreenRect>
        {
            [1] = Rect(6, 252, 760, 68), [2] = Rect(1282, 252, 760, 68),
            [3] = Rect(6, 670, 760, 68), [4] = Rect(1282, 670, 760, 68),
        }, plan.SideMeters);
        Assert.Null(WaveformOverlayPlan.Create(Layout(FourDeckVertical), WaveformMode.Normal, showsMeters: false, Band));
    }

    [Fact]
    public void TwoDeckVerticalMetersFlankTheColumns()
    {
        var plan = Plan(TwoDeckVertical, WaveformMode.HideBoth);

        Assert.Equal(Rect(776, 146, 496, 566), plan.Panel);
        Assert.Equal([Rect(0, 0, 248, 566), Rect(248, 0, 248, 566)], plan.Panes.Select(pane => pane.Frame));
        Assert.Equal(new Dictionary<int, ScreenRect> { [1] = Rect(6, 252, 760, 68), [2] = Rect(1282, 252, 760, 68) }, plan.SideMeters);
    }

    /// The divider and the band height keep their size in points: at 100% the plan is the 200% plan halved.
    [Fact]
    public void AtOneHundredPercentThePlanIsHalved()
    {
        static ScreenRect Half(ScreenRect rect) => new(rect.X / 2, rect.Y / 2, rect.Width / 2, rect.Height / 2);
        var full = Snapshot(FourDeckHorizontal);
        var layout = RekordboxLayout.Locate(full with
        {
            WindowFrame = Half(full.WindowFrame),
            Elements = full.Elements.Select(element => element with { Frame = Half(element.Frame) }).ToList(),
            Scale = 1,
        });
        var halfBand = new ScreenSize(Band.Width / 2, Band.Height / 2);

        var split = WaveformOverlayPlan.Create(layout, WaveformMode.Split, showsMeters: true, halfBand)!;
        var normal = WaveformOverlayPlan.Create(layout, WaveformMode.Normal, showsMeters: true, halfBand)!;

        Assert.Equal(1, layout.Scale);
        Assert.Equal([Rect(511, 0, 2, 194), Rect(0, 96, 1024, 2)], split.Dividers);
        Assert.Equal(Rect(0, 98, 511, 96), split.Panes[2].Frame);
        Assert.Equal(Rect(322, 0, 380, 28.8), normal.Bands[1]);
    }

    [Fact]
    public void PlansWithTheSameGeometryAreEqual()
    {
        Assert.Equal(Plan(FourDeckHorizontal, WaveformMode.Normal), Plan(FourDeckHorizontal, WaveformMode.Normal));
        Assert.NotEqual(Plan(FourDeckHorizontal, WaveformMode.Normal), Plan(FourDeckHorizontal, WaveformMode.Split));
    }
}
