namespace Earmatch.Core;

public enum BoothPreset
{
    Cdj3000,
    Cdj2000NXS2,
    Xdj1000,
}

public static class BoothPresetMembers
{
    extension(BoothPreset preset)
    {
        public PhaseMeterType PhaseMeterType => preset switch
        {
            BoothPreset.Cdj3000 => PhaseMeterType.Cdj3000Waveform,
            _ => PhaseMeterType.Box,
        };

        public CounterStyle CounterStyle => CounterStyle.CueCountdown;
        public BPMLimit BPMLimit => BPMLimit.OneDecimal;
        public WaveformMode WaveformMode => WaveformMode.Split;

        public void Apply(ref OverlaySettings settings)
        {
            settings.IsPhaseMeterEnabled = true;
            settings.PhaseMeterType = preset.PhaseMeterType;
            settings.CounterStyle = preset.CounterStyle;
            settings.IsBPMLimitEnabled = true;
            settings.BPMLimit = preset.BPMLimit;
            settings.WaveformMode = preset.WaveformMode;
            settings.BoothPreset = preset;
        }

        public bool Matches(OverlaySettings settings) =>
            settings.IsPhaseMeterEnabled && settings.PhaseMeterType == preset.PhaseMeterType && settings.CounterStyle == preset.CounterStyle
            && settings.IsBPMLimitEnabled && settings.BPMLimit == preset.BPMLimit && settings.WaveformMode == preset.WaveformMode;
    }

    extension(BoothPreset)
    {
        public static IReadOnlyList<BoothPreset> AllCases => Enum.GetValues<BoothPreset>();

        public static BoothPreset? Selected(OverlaySettings settings)
        {
            if (settings.BoothPreset is { } chosen && chosen.Matches(settings)) return chosen;
            var matching = BoothPreset.AllCases.Where(preset => preset.Matches(settings)).ToList();
            return matching.Count == 1 ? matching[0] : null;
        }
    }
}
