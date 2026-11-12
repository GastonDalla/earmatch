using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class BoothPresetTests
{
    [Fact]
    public void TheCDJ3000BoothUsesThePlayersDefaultWaveformMeter()
    {
        var settings = new OverlaySettings(isBPMLimitEnabled: false, bpmLimit: BPMLimit.CoverFully, waveformMode: WaveformMode.Normal,
            isPhaseMeterEnabled: false, phaseMeterType: PhaseMeterType.Box);
        BoothPreset.Cdj3000.Apply(ref settings);

        Assert.True(settings.IsPhaseMeterEnabled);
        Assert.Equal(PhaseMeterType.Cdj3000Waveform, settings.PhaseMeterType);
        Assert.Equal(CounterStyle.CueCountdown, settings.CounterStyle);
        Assert.True(settings.IsBPMLimitEnabled);
        Assert.Equal(BPMLimit.OneDecimal, settings.BPMLimit);
        Assert.Equal(WaveformMode.Split, settings.WaveformMode);
        Assert.Equal(BoothPreset.Cdj3000, settings.BoothPreset);
    }

    [Fact]
    public void TheNXS2AndXDJ1000BoothsUseTheBoxMeter()
    {
        foreach (var preset in new[] { BoothPreset.Cdj2000NXS2, BoothPreset.Xdj1000 })
        {
            var settings = new OverlaySettings(bpmLimit: BPMLimit.NoDecimals, waveformMode: WaveformMode.HideBoth,
                phaseMeterType: PhaseMeterType.Cdj3000PhaseMeter);
            preset.Apply(ref settings);

            Assert.Equal(PhaseMeterType.Box, settings.PhaseMeterType);
            Assert.Equal(CounterStyle.CueCountdown, settings.CounterStyle);
            Assert.Equal(BPMLimit.OneDecimal, settings.BPMLimit);
            Assert.Equal(WaveformMode.Split, settings.WaveformMode);
            Assert.Equal(preset, settings.BoothPreset);
        }
    }

    [Fact]
    public void ApplyingAPresetLeavesEarmatchsOtherSettingsAlone()
    {
        var settings = new OverlaySettings(isEnabled: false) { IsBlindModeEnabled = true };
        BoothPreset.Xdj1000.Apply(ref settings);

        Assert.False(settings.IsEnabled);
        Assert.True(settings.IsBlindModeEnabled);
    }

    [Fact]
    public void ThePresetShowsAsSelectedWhileTheSettingsMatchIt()
    {
        var settings = new OverlaySettings();
        BoothPreset.Cdj3000.Apply(ref settings);
        Assert.Equal(BoothPreset.Cdj3000, BoothPreset.Selected(settings));

        settings.WaveformMode = WaveformMode.Normal;
        Assert.Null(BoothPreset.Selected(settings));

        settings.WaveformMode = WaveformMode.Split;
        Assert.Equal(BoothPreset.Cdj3000, BoothPreset.Selected(settings));
    }

    [Fact]
    public void BoothsWithTheSameSettingsShowTheOneThatWasChosen()
    {
        var settings = new OverlaySettings();
        BoothPreset.Xdj1000.Apply(ref settings);
        Assert.Equal(BoothPreset.Xdj1000, BoothPreset.Selected(settings));

        BoothPreset.Cdj2000NXS2.Apply(ref settings);
        Assert.Equal(BoothPreset.Cdj2000NXS2, BoothPreset.Selected(settings));
    }

    [Fact]
    public void SettingsMadeByHandSelectTheOnlyBoothTheyMatch()
    {
        var settings = new OverlaySettings(bpmLimit: BPMLimit.OneDecimal, waveformMode: WaveformMode.Split,
            phaseMeterType: PhaseMeterType.Cdj3000Waveform) { CounterStyle = CounterStyle.CueCountdown };
        Assert.Equal(BoothPreset.Cdj3000, BoothPreset.Selected(settings));

        settings.PhaseMeterType = PhaseMeterType.Box;
        Assert.Null(BoothPreset.Selected(settings));
    }

    [Fact]
    public void ABoothNeedsTheMeterAndTheLimitSwitchedOn()
    {
        var settings = new OverlaySettings();
        BoothPreset.Cdj3000.Apply(ref settings);
        settings.IsPhaseMeterEnabled = false;
        Assert.Null(BoothPreset.Selected(settings));

        settings.IsPhaseMeterEnabled = true;
        settings.IsBPMLimitEnabled = false;
        Assert.Null(BoothPreset.Selected(settings));
    }
}
