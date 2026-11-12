using Earmatch.Core;

namespace Earmatch.Core.Tests;

/// Windows needs no Screen Recording grant, so the macOS "Allow Screen Recording" case is gone.
public class StatusTextLiveTests
{
    static StatusContext Context(OverlayActivation? activation = null, OverlaySettings? settings = null, string? captureProblem = null) =>
        new(activation ?? OverlayActivation.Active, settings ?? new OverlaySettings(), "7.2.19.0342", captureProblem);

    static readonly LiveDecksSnapshot TwoDecks = new(
        [new LiveDeckSnapshot(1, "Track A", IsMaster: true, Meter: null), new LiveDeckSnapshot(2, "Track B", IsMaster: false, Meter: null)],
        FramesPerSecond: 112.4, LatencyMilliseconds: 2.06);

    [Fact]
    public void ShowsFrameRateAndLatencyWhileTheDecksAreMirrored()
    {
        Assert.Equal(new StatusLine(StatusTone.Live, "Live · 112 fps · 2.1 ms"), StatusText.LiveDecks(Context(), TwoDecks));
    }

    [Fact]
    public void LeavesOutMeasurementsThePipelineHasNotMadeYet()
    {
        Assert.Equal(new StatusLine(StatusTone.Live, "Live"), StatusText.LiveDecks(Context(), TwoDecks with { FramesPerSecond = 0, LatencyMilliseconds = 0 }));
        Assert.Equal(new StatusLine(StatusTone.Live, "Live · 112 fps"), StatusText.LiveDecks(Context(), TwoDecks with { LatencyMilliseconds = 0 }));
        Assert.Equal(new StatusLine(StatusTone.Live, "Live · 2.1 ms"), StatusText.LiveDecks(Context(), TwoDecks with { FramesPerSecond = 0 }));
    }

    [Fact]
    public void IsOffWhileEarmatchIsOff()
    {
        Assert.Equal(new StatusLine(StatusTone.Off, "Earmatch is off"),
            StatusText.LiveDecks(Context(settings: new OverlaySettings(isEnabled: false)), TwoDecks));
    }

    [Fact]
    public void SaysWhatItIsWaitingForWhileRekordboxIsNotReady()
    {
        Assert.Equal(new StatusLine(StatusTone.Waiting, "Paused until rekordbox is in front"),
            StatusText.LiveDecks(Context(OverlayActivation.RekordboxNotFrontmost), TwoDecks));
    }

    [Fact]
    public void ReportsACaptureProblem()
    {
        Assert.Equal(new StatusLine(StatusTone.Waiting, "rekordbox window not found"),
            StatusText.LiveDecks(Context(captureProblem: "rekordbox window not found"), TwoDecks));
    }

    [Fact]
    public void WaitsForTheFirstSnapshotWithDecks()
    {
        Assert.Equal(new StatusLine(StatusTone.Waiting, "Reading the decks…"), StatusText.LiveDecks(Context(), null));
        Assert.Equal(new StatusLine(StatusTone.Waiting, "Reading the decks…"), StatusText.LiveDecks(Context(), LiveDecksSnapshot.Empty));
    }

    [Fact]
    public void TheToneOnlyCheckAgreesWithTheLine()
    {
        var activations = new[]
        {
            OverlayActivation.RekordboxNotRunning, OverlayActivation.MainWindowNotFound, OverlayActivation.RekordboxNotFrontmost,
            OverlayActivation.WrongMode(RekordboxMode.Export), OverlayActivation.WrongLayout("Browse"), OverlayActivation.WrongLayout(null),
            OverlayActivation.DecksNotFound, OverlayActivation.Active,
        };
        LiveDecksSnapshot?[] snapshots =
        [
            null, LiveDecksSnapshot.Empty,
            new([new LiveDeckSnapshot(1, null, false, null)], 0, 0),
            new([new LiveDeckSnapshot(1, null, false, null)], 112, 2.1),
        ];
        foreach (var activation in activations)
        {
            foreach (var settings in new[] { new OverlaySettings(), new OverlaySettings(isEnabled: false) })
            {
                foreach (var problem in new string?[] { null, "", "frame too small" })
                {
                    var context = new StatusContext(activation, settings, null, problem);
                    foreach (var snapshot in snapshots)
                    {
                        Assert.Equal(StatusText.LiveDecks(context, snapshot).Tone == StatusTone.Live, StatusText.IsMirroringDecks(context, snapshot));
                    }
                }
            }
        }
    }

    [Fact]
    public void EveryLiveDecksLineGoesThroughTheLookup()
    {
        var lines = Localized.Marked(() => new[]
        {
            StatusText.LiveDecks(Context(), TwoDecks).Text,
            StatusText.LiveDecks(Context(), TwoDecks with { FramesPerSecond = 0, LatencyMilliseconds = 0 }).Text,
            StatusText.LiveDecks(Context(), null).Text,
        });
        Assert.All(lines, line => Assert.True(Localized.IsLocalized(line), line));
    }
}
