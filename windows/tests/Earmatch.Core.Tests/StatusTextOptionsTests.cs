using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class StatusTextOptionsTests
{
    [Fact]
    public void PhaseMeterTypesNameThePlayersTheyCopy()
    {
        Assert.Equal("Box · CDJ-2000NXS / XDJ-1000 (NXS2 Type 1)", PhaseMeterType.Box.Explanation);
        Assert.Equal("Line · CDJ-2000NXS2 / XDJ-1000MK2 Type 2", PhaseMeterType.Line.Explanation);
        Assert.Equal("CDJ-3000 · phase meter, counters on the left", PhaseMeterType.Cdj3000PhaseMeter.Explanation);
        Assert.Equal("CDJ-3000 · master waveform above yours (default)", PhaseMeterType.Cdj3000Waveform.Explanation);
    }

    [Fact]
    public void CounterStylesSayWhatTheyCount()
    {
        Assert.Equal("Countdown to next cue (players)", CounterStyle.CueCountdown.Title);
        Assert.Equal("Bar position (rekordbox)", CounterStyle.BarPosition.Title);
        Assert.Equal("Bars and beats to the next cue ahead; --.- when none", CounterStyle.CueCountdown.Explanation);
        Assert.Equal("The bar and beat at the playhead, like rekordbox", CounterStyle.BarPosition.Explanation);
    }

    [Fact]
    public void BoothPresetsNameTheirPlayersAndWhatTheySet()
    {
        Assert.Equal(["CDJ-3000", "CDJ-2000NXS2", "XDJ-1000"], BoothPreset.AllCases.Select(preset => preset.DisplayName));
        Assert.Equal("CDJ-3000 · waveform meter, cue countdown, one decimal", BoothPreset.Cdj3000.Explanation);
        Assert.Equal("CDJ-2000NXS2 · box meter, cue countdown, one decimal", BoothPreset.Cdj2000NXS2.Explanation);
        Assert.Equal("XDJ-1000 · box meter, cue countdown, one decimal", BoothPreset.Xdj1000.Explanation);
        Assert.Equal("Custom · your own mix of settings", BoothPreset.CustomExplanation);
    }

    [Fact]
    public void TheMixTrainerSwitchSaysWhatItKeepsRecording()
    {
        Assert.Equal("Mix trainer", OptionText.MixTrainerTitle);
        Assert.Equal("Records your mixes even with the overlays off", OptionText.MixTrainerSubtitle);
        Assert.True(OptionText.MixTrainerSubtitle.Length <= 60);
    }

    [Fact]
    public void BlindModeSaysWhatItCovers()
    {
        Assert.Equal("Full blind mode", OptionText.BlindModeTitle);
        Assert.Equal("Also covers the pitch % and the deck header BPM", OptionText.BlindModeSubtitle);
    }

    [Fact]
    public void WaveformModesSayWhatTheyDoToTheStackedWaveform()
    {
        Assert.Equal("Normal · rekordbox's stacked waveforms, unchanged", WaveformMode.Normal.Explanation);
        Assert.Equal("Split · each deck in its own half, like separate CDJ screens", WaveformMode.Split.Explanation);
        Assert.Equal("Hide Both · no waveforms, only the phase meters", WaveformMode.HideBoth.Explanation);
    }

    [Fact]
    public void BpmLimitsShowWhatTheJogReads()
    {
        Assert.Equal("One decimal · 127.22 reads 127.2", BPMLimit.OneDecimal.Explanation);
        Assert.Equal("No decimals · 127.22 reads 127", BPMLimit.NoDecimals.Explanation);
        Assert.Equal("Cover fully · the jog BPM is hidden", BPMLimit.CoverFully.Explanation);
    }

    [Fact]
    public void OptionsHaveShortNamesForTheMenus()
    {
        Assert.Equal(["One decimal", "No decimals", "Cover fully"], BPMLimit.AllCases.Select(limit => limit.DisplayName));
        Assert.Equal(["Normal", "Split", "Hide Both"], WaveformMode.AllCases.Select(mode => mode.DisplayName));
        Assert.Equal(["Box", "Line", "3000 Meter", "3000 Wave"], PhaseMeterType.AllCases.Select(type => type.DisplayName));
        Assert.Equal(["Cue countdown", "Bar position"], CounterStyle.AllCases.Select(style => style.DisplayName));
        Assert.Equal("CDJ-3000 Phase meter", PhaseMeterType.Cdj3000PhaseMeter.Title);
    }

    /// English keeps the macOS limit of 60 characters; the Spanish texts of the macOS catalog run up to 65.
    [Theory]
    [InlineData("en", 60)]
    [InlineData("es", 65)]
    public void EveryExplanationFitsOneShortLine(string language, int maxLength)
    {
        var all = L10n.WithOverride(LocalizationCatalog.Load(language), () =>
            BPMLimit.AllCases.Select(limit => limit.Explanation)
                .Concat(WaveformMode.AllCases.Select(mode => mode.Explanation))
                .Concat(PhaseMeterType.AllCases.Select(type => type.Explanation))
                .Concat(CounterStyle.AllCases.Select(style => style.Explanation))
                .Concat(BoothPreset.AllCases.Select(preset => preset.Explanation))
                .Concat([BoothPreset.CustomExplanation, OptionText.BlindModeSubtitle])
                .ToList());
        foreach (var text in all)
        {
            Assert.True(text.Length <= maxLength, text);
            Assert.DoesNotContain("\n", text);
        }
    }
}
