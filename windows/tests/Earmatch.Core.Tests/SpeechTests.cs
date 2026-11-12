using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class SpeechTests
{
    static string Spanish(Func<string> text) => L10n.WithOverride(LocalizationCatalog.Load("es"), text);

    static string English(Func<string> text) => L10n.WithOverride(LocalizationCatalog.Load("en"), text);

    static PhaseMeterRow Row(PhaseMeterRole role, int deck, int? beat, bool? onMastersBeat, string counter) =>
        new(role, deck, false, beat, null, onMastersBeat, [], [], PhaseMeterTickDirection.Up, counter);

    static PhaseMeterDisplay Meter(params PhaseMeterRow[] rows) =>
        new(PhaseMeterType.Box, 1, null, PhaseMeterCounterSide.Left, PhaseMeterModel.LineSpan, [.. rows]);

    [Fact]
    public void DeckRow_ReadsTheDeckTitleBeatPhaseAndCounter()
    {
        var deck = new LiveDeckSnapshot(2, "Strobe", false, Meter(Row(PhaseMeterRole.Master, 1, 0, null, "12.1"), Row(PhaseMeterRole.Deck, 2, 2, false, "08.3")));
        Assert.Equal("Deck 2, Strobe, beat 3 of 4, out of phase with deck 1, 08.3 bars", English(() => MeterSpeech.DeckRow(deck, showsMeter: true)));
        Assert.Equal("Deck 2, Strobe, beat 3 de 4, fuera de fase con el deck 1, 08.3 compases", Spanish(() => MeterSpeech.DeckRow(deck, showsMeter: true)));
    }

    [Fact]
    public void DeckRow_NamesTheSyncMasterAndAnEmptyDeckAndSkipsAMeterThatIsOff()
    {
        var master = new LiveDeckSnapshot(1, null, true, Meter(Row(PhaseMeterRole.Deck, 1, 0, null, "01.1")));
        Assert.Equal("Deck 1, no track loaded, Sync Master", English(() => MeterSpeech.DeckRow(master, showsMeter: false)));
        Assert.Equal("Deck 1, no track loaded, Sync Master, beat 1 of 4, 01.1 bars", English(() => MeterSpeech.DeckRow(master, showsMeter: true)));
    }

    [Fact]
    public void DeckRow_LeavesOutTheBeatOfAGridNotReadYet()
    {
        var deck = new LiveDeckSnapshot(3, "Track", false, Meter(Row(PhaseMeterRole.Deck, 3, null, null, PhaseMeterDisplay.EmptyCounter)));
        Assert.Equal("Deck 3, Track, --.- bars", English(() => MeterSpeech.DeckRow(deck, showsMeter: true)));
    }

    [Fact]
    public void Live_ReadsTheMixTheTempoAndThePhaseError()
    {
        var matching = new PracticeLiveStatus(1, 2, 70, 70, -0.5, null, null, null);
        Assert.Equal("Mixing deck 1 into deck 2 for 1 minute, 10 seconds. tempos +0.50 BPM apart", English(() => PracticeSpeech.Live(matching)));
        var matched = matching with { SecondsToMatch = 12, PhaseErrorMilliseconds = -8 };
        Assert.Equal("Mezclando el deck 1 con el deck 2 durante 1 minuto y 10 segundos. tempos ajustados después de 12 segundos. error de fase 8 ms atrasado",
            Spanish(() => PracticeSpeech.Live(matched)));
    }

    [Fact]
    public void Summary_ReadsEveryMetricOfTheLastMix()
    {
        var mix = new PracticeMix(Guid.Empty, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1, 2, 120, 20, 0.04, 6.4, 21);
        Assert.Equal("Last mix, Deck 1 → Deck 2, 2 minutes long, tempos matched after 20 seconds, drift +0.04 BPM, average phase error 6 ms, worst 21 ms",
            English(() => PracticeSpeech.Summary(mix)));
        var unmatched = mix with { SecondsToMatch = null, TempoDriftBPM = null, MeanPhaseErrorMilliseconds = null, WorstPhaseErrorMilliseconds = null };
        Assert.Equal("Última mezcla, Deck 1 → Deck 2, de 2 minutos, los tempos nunca coincidieron", Spanish(() => PracticeSpeech.Summary(unmatched)));
    }

    [Fact]
    public void Replay_ReadsTheTransitionAndTheCursor()
    {
        var trace = new PracticeTrace(Guid.Empty, 61, 9, [new PracticeTraceSample(9, 3), new PracticeTraceSample(30, -14), new PracticeTraceSample(60, 1)]);
        Assert.Equal("Phase error of the last transition, 1 minute, 1 second long, tempos matched at 0:09, worst 14 ms behind at 0:30",
            English(() => PracticeSpeech.Replay(trace)));
        Assert.Equal("0:30, 14 ms behind", English(() => PracticeSpeech.ReplayValue(30, -14)));
        Assert.Equal("0:05", English(() => PracticeSpeech.ReplayValue(5, null)));
        Assert.Equal("en fase", Spanish(() => PracticeSpeech.Error(0.3)));
        Assert.Equal("4 ms adelantado", Spanish(() => PracticeSpeech.Error(4.2)));
    }
}
