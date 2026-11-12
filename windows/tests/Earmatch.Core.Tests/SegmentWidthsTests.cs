using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class SegmentWidthsTests
{
    [Fact]
    public void LabelsThatFitShareTheRowEqually()
    {
        Assert.Equal([100, 100, 100], SegmentWidths.Distribute([40, 52, 61], 300));
    }

    [Fact]
    public void ALongLabelKeepsItsWidthAndTheOthersShareTheRest()
    {
        var widths = SegmentWidths.Distribute([40, 40, 90, 70], 290);
        Assert.Equal([65, 65, 90, 70], widths);
        Assert.Equal(290, widths.Sum());
    }

    [Fact]
    public void WhenNothingFitsEverySegmentShrinksInProportion()
    {
        Assert.Equal([50, 25, 25], SegmentWidths.Distribute([100, 50, 50], 100));
    }

    [Fact]
    public void NoSegmentsOrNoRoomGiveNoWidth()
    {
        Assert.Empty(SegmentWidths.Distribute([], 200));
        Assert.Equal([0, 0], SegmentWidths.Distribute([30, 40], 0));
        Assert.Equal([0, 0], SegmentWidths.Distribute([30, 40], -10));
    }
}
