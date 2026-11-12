using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class LiveDecksSnapshotTests
{
    [Fact]
    public void APausedDeckIsNotPlaying()
    {
        var deck = new LiveDeckSnapshot(1, null, IsMaster: false, Meter: null, PhaseInBar: 1.5, BeatsPerSecond: 0);
        Assert.False(deck.IsPlaying);
    }

    [Fact]
    public void ADeckAtTempoIsPlaying()
    {
        var deck = new LiveDeckSnapshot(1, null, IsMaster: true, Meter: null, PhaseInBar: 3.9, BeatsPerSecond: 2.25);
        Assert.True(deck.IsPlaying);
        Assert.Equal(135, deck.BeatsPerSecond * 60);
    }

    [Fact]
    public void NewFieldsDefaultToUnknownAndPaused()
    {
        var deck = new LiveDeckSnapshot(2, "Track", IsMaster: false, Meter: null);
        Assert.Null(deck.PhaseInBar);
        Assert.Equal(0, deck.BeatsPerSecond);
        Assert.False(deck.IsPlaying);
    }

    [Fact]
    public void SnapshotsWithTheSameDecksAreEqual()
    {
        LiveDecksSnapshot Make() => new([new LiveDeckSnapshot(1, "A", true, null)], 60, 7.4);
        Assert.Equal(Make(), Make());
        Assert.NotEqual(Make(), LiveDecksSnapshot.Empty);
    }
}
