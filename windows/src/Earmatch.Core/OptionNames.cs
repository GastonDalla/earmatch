namespace Earmatch.Core;

public static class OptionNames
{
    extension(BPMLimit limit)
    {
        public string DisplayName => limit switch
        {
            BPMLimit.OneDecimal => L10n.Text("One decimal"),
            BPMLimit.NoDecimals => L10n.Text("No decimals"),
            _ => L10n.Text("Cover fully"),
        };
    }

    extension(WaveformMode mode)
    {
        public string DisplayName => mode switch
        {
            WaveformMode.Normal => L10n.Text("Normal"),
            WaveformMode.Split => L10n.Text("Split"),
            _ => L10n.Text("Hide Both"),
        };
    }

    extension(PhaseMeterType type)
    {
        public string DisplayName => type switch
        {
            PhaseMeterType.Box => L10n.Text("Box"),
            PhaseMeterType.Line => L10n.Text("Line"),
            PhaseMeterType.Cdj3000PhaseMeter => L10n.Text("3000 Meter"),
            _ => L10n.Text("3000 Wave"),
        };

        public string Title => type switch
        {
            PhaseMeterType.Box => L10n.Text("Box (CDJ-2000NXS / XDJ-1000)"),
            PhaseMeterType.Line => L10n.Text("Line (CDJ-2000NXS2 Type 2)"),
            PhaseMeterType.Cdj3000PhaseMeter => L10n.Text("CDJ-3000 Phase meter"),
            _ => L10n.Text("CDJ-3000 Waveform"),
        };
    }

    extension(CounterStyle style)
    {
        public string DisplayName => style switch
        {
            CounterStyle.CueCountdown => L10n.Text("Cue countdown"),
            _ => L10n.Text("Bar position"),
        };

        public string Title => style switch
        {
            CounterStyle.CueCountdown => L10n.Text("Countdown to next cue (players)"),
            _ => L10n.Text("Bar position (rekordbox)"),
        };
    }
}
