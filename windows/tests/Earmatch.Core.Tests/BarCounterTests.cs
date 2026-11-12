using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class BarCounterTests
{
    [Fact]
    public void MatchesTheBarTextRekordboxDrawsNearThePlayhead()
    {
        Assert.Equal("06.3", BarCounter.Position(elapsedSeconds: 10.8, bpm: 124, phaseInBar: 2.3).Label);
        Assert.Equal("24.3", BarCounter.Position(elapsedSeconds: 42.0, bpm: 135, phaseInBar: 2.5).Label);
    }

    [Fact]
    public void TheNextBarStartsWhenThePixelDownbeatPassesEvenIfTheClockLags()
    {
        Assert.Equal("07.1", BarCounter.Position(elapsedSeconds: 11.5, bpm: 124, phaseInBar: 0.1).Label);
    }

    [Fact]
    public void TheBarHoldsUntilThePixelDownbeatEvenIfTheClockRunsAhead()
    {
        Assert.Equal("06.4", BarCounter.Position(elapsedSeconds: 11.7, bpm: 124, phaseInBar: 3.9).Label);
    }

    [Fact]
    public void WithoutAPixelReadingTheClockDecides()
    {
        Assert.Equal("06.3", BarCounter.Position(elapsedSeconds: 10.8, bpm: 124, phaseInBar: null).Label);
    }

    [Fact]
    public void TheFirstBeatIsBarOneBeatOne()
    {
        var start = BarCounter.Position(elapsedSeconds: 0, bpm: 124, phaseInBar: 0);
        Assert.Equal(new BarPosition(Bar: 1, Beat: 1), start);
        Assert.Equal("01.1", start.Label);
    }
}
