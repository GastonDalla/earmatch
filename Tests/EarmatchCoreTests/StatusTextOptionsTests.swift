import Testing
import EarmatchCore

@Suite struct StatusTextOptionsTests {
    @Test func phaseMeterTypesNameThePlayersTheyCopy() {
        #expect(PhaseMeterType.box.explanation == "Box · CDJ-2000NXS / XDJ-1000 (NXS2 Type 1)")
        #expect(PhaseMeterType.line.explanation == "Line · CDJ-2000NXS2 / XDJ-1000MK2 Type 2")
        #expect(PhaseMeterType.cdj3000PhaseMeter.explanation == "CDJ-3000 · phase meter, counters on the left")
        #expect(PhaseMeterType.cdj3000Waveform.explanation == "CDJ-3000 · master waveform above yours (default)")
    }

    @Test func counterStylesSayWhatTheyCount() {
        #expect(CounterStyle.cueCountdown.title == "Countdown to next cue (players)")
        #expect(CounterStyle.barPosition.title == "Bar position (rekordbox)")
        #expect(CounterStyle.cueCountdown.explanation == "Bars and beats to the next cue ahead; --.- when none")
        #expect(CounterStyle.barPosition.explanation == "The bar and beat at the playhead, like rekordbox")
    }

    @Test func boothPresetsNameTheirPlayersAndWhatTheySet() {
        #expect(BoothPreset.allCases.map(\.displayName) == ["CDJ-3000", "CDJ-2000NXS2", "XDJ-1000"])
        #expect(BoothPreset.cdj3000.explanation == "CDJ-3000 · waveform meter, cue countdown, one decimal")
        #expect(BoothPreset.cdj2000NXS2.explanation == "CDJ-2000NXS2 · box meter, cue countdown, one decimal")
        #expect(BoothPreset.xdj1000.explanation == "XDJ-1000 · box meter, cue countdown, one decimal")
        #expect(BoothPreset.customExplanation == "Custom · your own mix of settings")
    }

    @Test func theMixTrainerSwitchSaysWhatItKeepsRecording() {
        #expect(OptionText.mixTrainerTitle == "Mix trainer")
        #expect(OptionText.mixTrainerSubtitle == "Records your mixes even with the overlays off")
        #expect(OptionText.mixTrainerSubtitle.count <= 60)
    }

    @Test func blindModeSaysWhatItCovers() {
        #expect(OptionText.blindModeTitle == "Full blind mode")
        #expect(OptionText.blindModeSubtitle == "Also covers the pitch % and the deck header BPM")
    }

    @Test func waveformModesSayWhatTheyDoToTheStackedWaveform() {
        #expect(WaveformMode.normal.explanation == "Normal · rekordbox's stacked waveforms, unchanged")
        #expect(WaveformMode.split.explanation == "Split · each deck in its own half, like separate CDJ screens")
        #expect(WaveformMode.hideBoth.explanation == "Hide Both · no waveforms, only the phase meters")
    }

    @Test func bpmLimitsShowWhatTheJogReads() {
        #expect(BPMLimit.oneDecimal.explanation == "One decimal · 127.22 reads 127.2")
        #expect(BPMLimit.noDecimals.explanation == "No decimals · 127.22 reads 127")
        #expect(BPMLimit.coverFully.explanation == "Cover fully · the jog BPM is hidden")
    }

    @Test func everyExplanationFitsOneShortLine() {
        let all = BPMLimit.allCases.map(\.explanation) + WaveformMode.allCases.map(\.explanation)
            + PhaseMeterType.allCases.map(\.explanation) + CounterStyle.allCases.map(\.explanation)
            + BoothPreset.allCases.map(\.explanation) + [BoothPreset.customExplanation, OptionText.blindModeSubtitle]
        for text in all {
            #expect(text.count <= 60, "\(text)")
            #expect(!text.contains("\n"))
        }
    }
}
