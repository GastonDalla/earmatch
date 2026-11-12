using System.Text.Json;
using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class OverlaySettingsTests
{
    static OverlaySettings Decode(string json) => JsonSerializer.Deserialize<OverlaySettings>(json);

    static OverlaySettings RoundTrip(OverlaySettings settings) => Decode(JsonSerializer.Serialize(settings));

    [Fact]
    public void DefaultSettingsMatchTheUsersClubModeSetup()
    {
        var settings = new OverlaySettings();
        Assert.True(settings.IsEnabled);
        Assert.True(settings.IsBPMLimitEnabled);
        Assert.Equal(BPMLimit.OneDecimal, settings.BPMLimit);
        Assert.Equal(WaveformMode.Split, settings.WaveformMode);
        Assert.True(settings.IsPhaseMeterEnabled);
        Assert.Equal(PhaseMeterType.Line, settings.PhaseMeterType);
    }

    [Fact]
    public void SettingsSurviveAJSONRoundTrip()
    {
        var original = new OverlaySettings(isEnabled: false, isBPMLimitEnabled: false, bpmLimit: BPMLimit.CoverFully,
            waveformMode: WaveformMode.HideBoth, isPhaseMeterEnabled: false, phaseMeterType: PhaseMeterType.Box);
        Assert.Equal(original, RoundTrip(original));
    }

    [Fact]
    public void SettingsSavedByAnOlderVersionKeepTheirValuesAndDefaultTheRest()
    {
        var decoded = Decode("""{"isEnabled":false,"bpmLimit":"noDecimals"}""");
        Assert.False(decoded.IsEnabled);
        Assert.Equal(BPMLimit.NoDecimals, decoded.BPMLimit);
        Assert.Equal(new OverlaySettings().WaveformMode, decoded.WaveformMode);
        Assert.Equal(new OverlaySettings().PhaseMeterType, decoded.PhaseMeterType);
    }

    [Fact]
    public void AnOptionFromANewerVersionFallsBackOnlyForThatSetting()
    {
        var decoded = Decode("""{"isEnabled":false,"bpmLimit":"twoDecimalsBlinking"}""");
        Assert.False(decoded.IsEnabled);
        Assert.Equal(new OverlaySettings().BPMLimit, decoded.BPMLimit);
    }

    [Fact]
    public void AValueOfTheWrongTypeFallsBackOnlyForThatSetting()
    {
        var decoded = Decode("""{"isEnabled":"no","waveformMode":"hideBoth","isPhaseMeterEnabled":1}""");
        Assert.True(decoded.IsEnabled);
        Assert.Equal(WaveformMode.HideBoth, decoded.WaveformMode);
        Assert.True(decoded.IsPhaseMeterEnabled);
    }

    [Fact]
    public void MeterStylesSavedBeforeTheCDJ3000StylesStillDecode()
    {
        Assert.Equal(PhaseMeterType.Box, Decode("""{"phaseMeterType":"box"}""").PhaseMeterType);
        Assert.Equal(PhaseMeterType.Line, Decode("""{"phaseMeterType":"line"}""").PhaseMeterType);
    }

    [Fact]
    public void EveryMeterStyleIsOffered()
    {
        Assert.Equal([PhaseMeterType.Box, PhaseMeterType.Line, PhaseMeterType.Cdj3000PhaseMeter, PhaseMeterType.Cdj3000Waveform],
            PhaseMeterType.AllCases);
        Assert.Equal([false, false, true, true], PhaseMeterType.AllCases.Select(type => type.IsCDJ3000));
    }

    [Fact]
    public void NewOptionsDefaultToTheBehaviourBeforeThem()
    {
        var settings = new OverlaySettings();
        Assert.Equal(CounterStyle.BarPosition, settings.CounterStyle);
        Assert.False(settings.IsBlindModeEnabled);
        Assert.Null(settings.BoothPreset);
    }

    [Fact]
    public void NewOptionsSurviveAJSONRoundTrip()
    {
        var original = new OverlaySettings(phaseMeterType: PhaseMeterType.Cdj3000Waveform)
        {
            CounterStyle = CounterStyle.CueCountdown,
            IsBlindModeEnabled = true,
            BoothPreset = BoothPreset.Cdj3000,
        };
        Assert.Equal(original, RoundTrip(original));
    }

    [Fact]
    public void SettingsSavedWithoutTheNewOptionsKeepTheirDefaults()
    {
        var decoded = Decode("""{"phaseMeterType":"line","counterStyle":"sideways"}""");
        Assert.Equal(PhaseMeterType.Line, decoded.PhaseMeterType);
        Assert.Equal(CounterStyle.BarPosition, decoded.CounterStyle);
        Assert.False(decoded.IsBlindModeEnabled);
        Assert.Null(decoded.BoothPreset);
    }

    [Fact]
    public void TheMixTrainerIsOnByDefaultAndWhenSavedSettingsLackIt()
    {
        Assert.True(new OverlaySettings().IsMixTrainerEnabled);
        Assert.True(Decode("""{"isEnabled":true,"phaseMeterType":"box"}""").IsMixTrainerEnabled);
    }

    [Fact]
    public void TheMixTrainerSwitchSurvivesAJSONRoundTrip()
    {
        var original = new OverlaySettings { IsMixTrainerEnabled = false };
        var decoded = RoundTrip(original);
        Assert.False(decoded.IsMixTrainerEnabled);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void SettingsAreSavedWithTheMacOSKeysAndRawValues()
    {
        var settings = new OverlaySettings(bpmLimit: BPMLimit.NoDecimals, phaseMeterType: PhaseMeterType.Cdj3000PhaseMeter)
        {
            BoothPreset = BoothPreset.Cdj2000NXS2,
        };
        var json = JsonSerializer.Serialize(settings);
        Assert.Equal(
            """{"isEnabled":true,"isBPMLimitEnabled":true,"bpmLimit":"noDecimals","waveformMode":"split","isPhaseMeterEnabled":true,"phaseMeterType":"cdj3000PhaseMeter","counterStyle":"barPosition","isBlindModeEnabled":false,"boothPreset":"cdj2000NXS2","isMixTrainerEnabled":true}""",
            json);
        Assert.DoesNotContain("boothPreset", JsonSerializer.Serialize(new OverlaySettings()));
    }
}
