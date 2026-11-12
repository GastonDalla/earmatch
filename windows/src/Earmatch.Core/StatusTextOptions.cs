namespace Earmatch.Core;

public static class OptionExplanations
{
    extension(BPMLimit limit)
    {
        public string Explanation => limit switch
        {
            BPMLimit.OneDecimal => L10n.Text("One decimal · 127.22 reads 127.2"),
            BPMLimit.NoDecimals => L10n.Text("No decimals · 127.22 reads 127"),
            _ => L10n.Text("Cover fully · the jog BPM is hidden"),
        };
    }

    extension(WaveformMode mode)
    {
        public string Explanation => mode switch
        {
            WaveformMode.Normal => L10n.Text("Normal · rekordbox's stacked waveforms, unchanged"),
            WaveformMode.Split => L10n.Text("Split · each deck in its own half, like separate CDJ screens"),
            _ => L10n.Text("Hide Both · no waveforms, only the phase meters"),
        };
    }

    extension(PhaseMeterType type)
    {
        public string Explanation => type switch
        {
            PhaseMeterType.Box => L10n.Text("Box · CDJ-2000NXS / XDJ-1000 (NXS2 Type 1)"),
            PhaseMeterType.Line => L10n.Text("Line · CDJ-2000NXS2 / XDJ-1000MK2 Type 2"),
            PhaseMeterType.Cdj3000PhaseMeter => L10n.Text("CDJ-3000 · phase meter, counters on the left"),
            _ => L10n.Text("CDJ-3000 · master waveform above yours (default)"),
        };
    }

    extension(CounterStyle style)
    {
        public string Explanation => style switch
        {
            CounterStyle.CueCountdown => L10n.Text("Bars and beats to the next cue ahead; --.- when none"),
            _ => L10n.Text("The bar and beat at the playhead, like rekordbox"),
        };
    }

    extension(BoothPreset preset)
    {
        public string DisplayName => preset switch
        {
            BoothPreset.Cdj3000 => "CDJ-3000",
            BoothPreset.Cdj2000NXS2 => "CDJ-2000NXS2",
            _ => "XDJ-1000",
        };

        public string Explanation => preset switch
        {
            BoothPreset.Cdj3000 => L10n.Text("CDJ-3000 · waveform meter, cue countdown, one decimal"),
            BoothPreset.Cdj2000NXS2 => L10n.Text("CDJ-2000NXS2 · box meter, cue countdown, one decimal"),
            _ => L10n.Text("XDJ-1000 · box meter, cue countdown, one decimal"),
        };
    }
}

public static class BoothPresetTexts
{
    extension(BoothPreset)
    {
        public static string CustomExplanation => L10n.Text("Custom · your own mix of settings");
    }
}

public static class OptionText
{
    public static string BoothTitle => L10n.Text("Booth");
    public static string BoothSectionTitle => L10n.Text("BOOTH");
    public static string BoothSubtitle => L10n.Text("Set everything like the players in the booth");
    public static string CounterTitle => L10n.Text("Counter");
    public static string BlindModeTitle => L10n.Text("Full blind mode");
    public static string BlindModeSubtitle => L10n.Text("Also covers the pitch % and the deck header BPM");
    public static string BlindModeMenuTitle => L10n.Text("Full Blind Mode");
    public static string CounterMenuTitle => L10n.Text("Counter");
    public static string BoothMenuTitle => L10n.Text("Booth");
    public static string PhaseMeterStyleMenuTitle => L10n.Text("Phase Meter Style");
    public static string MixTrainerTitle => L10n.Text("Mix trainer");
    public static string MixTrainerSubtitle => L10n.Text("Records your mixes even with the overlays off");
    public static string MixTrainerMenuTitle => L10n.Text("Mix Trainer");
    public static string NoOtherMaster => L10n.Text("No other master");
}
