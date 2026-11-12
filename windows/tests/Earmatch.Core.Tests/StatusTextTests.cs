using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class StatusTextTests
{
    static StatusContext Context(OverlayActivation? activation = null, OverlaySettings? settings = null, string? version = "7.2.19.0342",
        string? captureProblem = null) =>
        new(activation ?? OverlayActivation.Active, settings ?? new OverlaySettings(), rekordboxVersion: version, captureProblem: captureProblem);

    [Fact]
    public void ConnectionWaitsForRekordbox()
    {
        Assert.Equal(new StatusLine(StatusTone.Waiting, "Waiting for rekordbox 7"), StatusText.Connection(Context(OverlayActivation.RekordboxNotRunning)));
    }

    [Fact]
    public void ConnectionNamesTheRunningRekordboxVersion()
    {
        var connected = new StatusLine(StatusTone.Live, "rekordbox 7.2.19.0342 connected");
        Assert.Equal(connected, StatusText.Connection(Context(OverlayActivation.Active)));
        Assert.Equal(connected, StatusText.Connection(Context(OverlayActivation.WrongLayout("Browse"))));
        Assert.Equal(new StatusLine(StatusTone.Live, "rekordbox connected"),
            StatusText.Connection(Context(OverlayActivation.RekordboxNotFrontmost, version: null)));
    }

    [Fact]
    public void EveryCardIsOffWhileEarmatchIsOff()
    {
        var off = Context(settings: new OverlaySettings(isEnabled: false));
        var expected = new StatusLine(StatusTone.Off, "Earmatch is off");
        Assert.Equal(expected, StatusText.BPMPrecision(off));
        Assert.Equal(expected, StatusText.Waveform(off));
        Assert.Equal(expected, StatusText.PhaseMeter(off));
    }

    [Fact]
    public void CardsSayWhatTheyAreWaitingFor()
    {
        var waiting = Context(OverlayActivation.WrongLayout(null));
        var expected = new StatusLine(StatusTone.Waiting, OverlayActivation.WrongLayout(null).Message);
        Assert.Equal(expected, StatusText.BPMPrecision(waiting));
        Assert.Equal(expected, StatusText.Waveform(waiting));
        Assert.Equal(expected, StatusText.PhaseMeter(waiting));
    }

    [Fact]
    public void BpmCardIsOffWhenTheLimitIsOff()
    {
        Assert.Equal(new StatusLine(StatusTone.Off, "Off: rekordbox shows the full BPM"),
            StatusText.BPMPrecision(Context(settings: new OverlaySettings(isBPMLimitEnabled: false))));
    }

    [Fact]
    public void BpmCardNamesTheLiveLimit()
    {
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: jog BPM shows one decimal"),
            StatusText.BPMPrecision(Context(settings: new OverlaySettings(bpmLimit: BPMLimit.OneDecimal))));
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: jog BPM shows no decimals"),
            StatusText.BPMPrecision(Context(settings: new OverlaySettings(bpmLimit: BPMLimit.NoDecimals))));
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: jog BPM covered"),
            StatusText.BPMPrecision(Context(settings: new OverlaySettings(bpmLimit: BPMLimit.CoverFully))));
    }

    [Fact]
    public void WaveformCardIsOffInNormalMode()
    {
        Assert.Equal(new StatusLine(StatusTone.Off, "Normal: rekordbox waveform unchanged"),
            StatusText.Waveform(Context(OverlayActivation.WrongLayout(null), settings: new OverlaySettings(waveformMode: WaveformMode.Normal))));
    }

    [Fact]
    public void WaveformCardNamesTheLiveMode()
    {
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: decks split, one playhead each"),
            StatusText.Waveform(Context(settings: new OverlaySettings(waveformMode: WaveformMode.Split))));
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: both waveforms hidden"),
            StatusText.Waveform(Context(settings: new OverlaySettings(waveformMode: WaveformMode.HideBoth))));
    }

    [Fact]
    public void PhaseMeterCardIsOffWhenTheMeterIsOff()
    {
        Assert.Equal(new StatusLine(StatusTone.Off, "Phase meter off"),
            StatusText.PhaseMeter(Context(settings: new OverlaySettings(isPhaseMeterEnabled: false))));
    }

    [Fact]
    public void PhaseMeterCardNamesTheLiveType()
    {
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: Box (CDJ-2000NXS / XDJ-1000)"),
            StatusText.PhaseMeter(Context(settings: new OverlaySettings(phaseMeterType: PhaseMeterType.Box))));
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: Line (CDJ-2000NXS2 Type 2)"),
            StatusText.PhaseMeter(Context(settings: new OverlaySettings(phaseMeterType: PhaseMeterType.Line))));
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: CDJ-3000 Phase meter"),
            StatusText.PhaseMeter(Context(settings: new OverlaySettings(phaseMeterType: PhaseMeterType.Cdj3000PhaseMeter))));
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: CDJ-3000 Waveform"),
            StatusText.PhaseMeter(Context(settings: new OverlaySettings(waveformMode: WaveformMode.Split, phaseMeterType: PhaseMeterType.Cdj3000Waveform))));
    }

    [Fact]
    public void TheCDJ3000WaveformSaysItNeedsSplitForTheMasterWaveform()
    {
        foreach (var mode in new[] { WaveformMode.Normal, WaveformMode.HideBoth })
        {
            Assert.Equal(new StatusLine(StatusTone.Live, "Live: CDJ-3000 Waveform · master waveform in Split only"),
                StatusText.PhaseMeter(Context(settings: new OverlaySettings(waveformMode: mode, phaseMeterType: PhaseMeterType.Cdj3000Waveform))));
        }
    }

    [Fact]
    public void BpmCardSaysWhatBlindModeCoversToo()
    {
        var blind = new OverlaySettings(bpmLimit: BPMLimit.OneDecimal) { IsBlindModeEnabled = true };
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: jog BPM shows one decimal; pitch and header BPM covered"),
            StatusText.BPMPrecision(Context(settings: blind)));

        blind.IsBPMLimitEnabled = false;
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: pitch and header BPM covered; jog BPM in full"),
            StatusText.BPMPrecision(Context(settings: blind)));
    }

    const string Problem = "Capture does not match the rekordbox window: frame 2048x1356, window 2560x1600";

    [Fact]
    public void ACaptureProblemStopsEveryFeatureThatNeedsPixelsFromClaimingLive()
    {
        var expected = new StatusLine(StatusTone.Waiting, Problem);
        var split = Context(settings: new OverlaySettings(waveformMode: WaveformMode.Split), captureProblem: Problem);
        Assert.Equal(expected, StatusText.Waveform(split));
        Assert.Equal(expected, StatusText.PhaseMeter(split));
        foreach (var limit in new[] { BPMLimit.OneDecimal, BPMLimit.NoDecimals })
        {
            Assert.Equal(expected, StatusText.BPMPrecision(Context(settings: new OverlaySettings(bpmLimit: limit), captureProblem: Problem)));
        }
    }

    [Fact]
    public void HideBothNeedsPixelsOnlyForItsPhaseMeters()
    {
        var withMeters = Context(settings: new OverlaySettings(waveformMode: WaveformMode.HideBoth, isPhaseMeterEnabled: true), captureProblem: Problem);
        Assert.Equal(new StatusLine(StatusTone.Waiting, Problem), StatusText.Waveform(withMeters));
        var withoutMeters = Context(settings: new OverlaySettings(waveformMode: WaveformMode.HideBoth, isPhaseMeterEnabled: false), captureProblem: Problem);
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: both waveforms hidden"), StatusText.Waveform(withoutMeters));
    }

    [Fact]
    public void FeaturesThatNeedNoPixelsStayLiveDuringACaptureProblem()
    {
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: jog BPM covered"),
            StatusText.BPMPrecision(Context(settings: new OverlaySettings(bpmLimit: BPMLimit.CoverFully), captureProblem: Problem)));
        Assert.Equal(new StatusLine(StatusTone.Off, "Normal: rekordbox waveform unchanged"),
            StatusText.Waveform(Context(settings: new OverlaySettings(waveformMode: WaveformMode.Normal), captureProblem: Problem)));
    }

    [Fact]
    public void ACaptureProblemDoesNotOverrideFeaturesThatAreOff()
    {
        Assert.Equal(new StatusLine(StatusTone.Off, "Phase meter off"),
            StatusText.PhaseMeter(Context(settings: new OverlaySettings(isPhaseMeterEnabled: false), captureProblem: Problem)));
        Assert.Equal(new StatusLine(StatusTone.Off, "Earmatch is off"),
            StatusText.Waveform(Context(settings: new OverlaySettings(isEnabled: false), captureProblem: Problem)));
    }

    [Fact]
    public void ABlankCaptureProblemCountsAsNone()
    {
        Assert.Equal(new StatusLine(StatusTone.Live, "Live: Box (CDJ-2000NXS / XDJ-1000)"),
            StatusText.PhaseMeter(Context(settings: new OverlaySettings(phaseMeterType: PhaseMeterType.Box), captureProblem: " \n ")));
    }

    [Fact]
    public void BpmSampleSplitsVisibleAndCoveredDigitsLikeTheJogMask()
    {
        Assert.Equal(new BPMSample(Visible: "127.2", Covered: "2"), new BPMSample(127.22, BPMLimit.OneDecimal));
        Assert.Equal(new BPMSample(Visible: "127", Covered: ".22"), new BPMSample(127.22, BPMLimit.NoDecimals));
        Assert.Equal(new BPMSample(Visible: "", Covered: "127.22"), new BPMSample(127.22, BPMLimit.CoverFully));
        Assert.Equal(new BPMSample(Visible: "127.22", Covered: ""), new BPMSample(127.22, null));
    }
}
