import Testing
import EarmatchCore

@Suite struct StatusTextTests {
    func context(
        _ activation: OverlayActivation = .active,
        settings: OverlaySettings = OverlaySettings(),
        screenRecording: Bool = true,
        version: String? = "7.2.19.0342",
        captureProblem: String? = nil
    ) -> StatusContext {
        StatusContext(activation: activation, settings: settings, isScreenRecordingGranted: screenRecording,
                      rekordboxVersion: version, captureProblem: captureProblem)
    }

    @Test func connectionAsksForAccessibilityFirst() {
        let line = StatusText.connection(context(.accessibilityMissing))
        #expect(line == StatusLine(tone: .waiting, text: OverlayActivation.accessibilityMissing.message))
    }

    @Test func connectionWaitsForRekordbox() {
        #expect(StatusText.connection(context(.rekordboxNotRunning)) == StatusLine(tone: .waiting, text: "Waiting for rekordbox 7"))
    }

    @Test func connectionNamesTheRunningRekordboxVersion() {
        let connected = StatusLine(tone: .live, text: "rekordbox 7.2.19.0342 connected")
        #expect(StatusText.connection(context(.active)) == connected)
        #expect(StatusText.connection(context(.wrongLayout("Browse"))) == connected)
        #expect(StatusText.connection(context(.rekordboxNotFrontmost, version: nil)) == StatusLine(tone: .live, text: "rekordbox connected"))
    }

    @Test func everyCardIsOffWhileEarmatchIsOff() {
        let off = context(settings: OverlaySettings(isEnabled: false))
        let expected = StatusLine(tone: .off, text: "Earmatch is off")
        #expect(StatusText.bpmPrecision(off) == expected)
        #expect(StatusText.waveform(off) == expected)
        #expect(StatusText.phaseMeter(off) == expected)
    }

    @Test func cardsSayWhatTheyAreWaitingFor() {
        let waiting = context(.wrongLayout(nil))
        let expected = StatusLine(tone: .waiting, text: OverlayActivation.wrongLayout(nil).message)
        #expect(StatusText.bpmPrecision(waiting) == expected)
        #expect(StatusText.waveform(waiting) == expected)
        #expect(StatusText.phaseMeter(waiting) == expected)
    }

    @Test func bpmCardIsOffWhenTheLimitIsOff() {
        let line = StatusText.bpmPrecision(context(settings: OverlaySettings(isBPMLimitEnabled: false)))
        #expect(line == StatusLine(tone: .off, text: "Off: rekordbox shows the full BPM"))
    }

    @Test func bpmCardNamesTheLiveLimit() {
        #expect(StatusText.bpmPrecision(context(settings: OverlaySettings(bpmLimit: .oneDecimal))) == StatusLine(tone: .live, text: "Live: jog BPM shows one decimal"))
        #expect(StatusText.bpmPrecision(context(settings: OverlaySettings(bpmLimit: .noDecimals))) == StatusLine(tone: .live, text: "Live: jog BPM shows no decimals"))
        #expect(StatusText.bpmPrecision(context(settings: OverlaySettings(bpmLimit: .coverFully))) == StatusLine(tone: .live, text: "Live: jog BPM covered"))
    }

    @Test func bpmCardWarnsThatWithoutScreenRecordingTheBPMIsCoveredFully() {
        let partial = context(settings: OverlaySettings(bpmLimit: .noDecimals), screenRecording: false)
        #expect(StatusText.bpmPrecision(partial) == StatusLine(tone: .waiting, text: "No Screen Recording: jog BPM covered fully"))
        let full = context(settings: OverlaySettings(bpmLimit: .coverFully), screenRecording: false)
        #expect(StatusText.bpmPrecision(full) == StatusLine(tone: .live, text: "Live: jog BPM covered"))
    }

    @Test func waveformCardIsOffInNormalMode() {
        let line = StatusText.waveform(context(.wrongLayout(nil), settings: OverlaySettings(waveformMode: .normal)))
        #expect(line == StatusLine(tone: .off, text: "Normal: rekordbox waveform unchanged"))
    }

    @Test func waveformCardNamesTheLiveMode() {
        #expect(StatusText.waveform(context(settings: OverlaySettings(waveformMode: .split))) == StatusLine(tone: .live, text: "Live: decks split, one playhead each"))
        #expect(StatusText.waveform(context(settings: OverlaySettings(waveformMode: .hideBoth))) == StatusLine(tone: .live, text: "Live: both waveforms hidden"))
    }

    @Test func splitNeedsScreenRecordingButHidingDoesNot() {
        let split = context(settings: OverlaySettings(waveformMode: .split), screenRecording: false)
        #expect(StatusText.waveform(split) == StatusLine(tone: .waiting, text: "Allow Screen Recording to split the waveforms"))
        let hidden = context(settings: OverlaySettings(waveformMode: .hideBoth), screenRecording: false)
        #expect(StatusText.waveform(hidden) == StatusLine(tone: .live, text: "Live: both waveforms hidden"))
    }

    @Test func phaseMeterCardIsOffWhenTheMeterIsOff() {
        let line = StatusText.phaseMeter(context(settings: OverlaySettings(isPhaseMeterEnabled: false)))
        #expect(line == StatusLine(tone: .off, text: "Phase meter off"))
    }

    @Test func phaseMeterCardNeedsScreenRecording() {
        let line = StatusText.phaseMeter(context(screenRecording: false))
        #expect(line == StatusLine(tone: .waiting, text: "Allow Screen Recording to read the beat grids"))
    }

    @Test func phaseMeterCardNamesTheLiveType() {
        #expect(StatusText.phaseMeter(context(settings: OverlaySettings(phaseMeterType: .box))) == StatusLine(tone: .live, text: "Live: Box (CDJ-2000NXS / XDJ-1000)"))
        #expect(StatusText.phaseMeter(context(settings: OverlaySettings(phaseMeterType: .line))) == StatusLine(tone: .live, text: "Live: Line (CDJ-2000NXS2 Type 2)"))
        #expect(StatusText.phaseMeter(context(settings: OverlaySettings(phaseMeterType: .cdj3000PhaseMeter))) == StatusLine(tone: .live, text: "Live: CDJ-3000 Phase meter"))
        #expect(StatusText.phaseMeter(context(settings: OverlaySettings(waveformMode: .split, phaseMeterType: .cdj3000Waveform))) == StatusLine(tone: .live, text: "Live: CDJ-3000 Waveform"))
    }

    @Test func theCDJ3000WaveformSaysItNeedsSplitForTheMasterWaveform() {
        for mode in [WaveformMode.normal, .hideBoth] {
            let line = StatusText.phaseMeter(context(settings: OverlaySettings(waveformMode: mode, phaseMeterType: .cdj3000Waveform)))
            #expect(line == StatusLine(tone: .live, text: "Live: CDJ-3000 Waveform · master waveform in Split only"))
        }
    }

    @Test func bpmCardSaysWhatBlindModeCoversToo() {
        var blind = OverlaySettings(bpmLimit: .oneDecimal)
        blind.isBlindModeEnabled = true
        #expect(StatusText.bpmPrecision(context(settings: blind)) == StatusLine(tone: .live, text: "Live: jog BPM shows one decimal; pitch and header BPM covered"))

        blind.isBPMLimitEnabled = false
        #expect(StatusText.bpmPrecision(context(settings: blind)) == StatusLine(tone: .live, text: "Live: pitch and header BPM covered; jog BPM in full"))
    }

    static let problem = "Capture does not match the rekordbox window: frame 1492x866, window 1600x900"

    @Test func aCaptureProblemStopsEveryFeatureThatNeedsPixelsFromClaimingLive() {
        let expected = StatusLine(tone: .waiting, text: Self.problem)
        let split = context(settings: OverlaySettings(waveformMode: .split), captureProblem: Self.problem)
        #expect(StatusText.waveform(split) == expected)
        #expect(StatusText.phaseMeter(split) == expected)
        for limit in [BPMLimit.oneDecimal, .noDecimals] {
            #expect(StatusText.bpmPrecision(context(settings: OverlaySettings(bpmLimit: limit), captureProblem: Self.problem)) == expected)
        }
    }

    @Test func hideBothNeedsPixelsOnlyForItsPhaseMeters() {
        let withMeters = context(settings: OverlaySettings(waveformMode: .hideBoth, isPhaseMeterEnabled: true), captureProblem: Self.problem)
        #expect(StatusText.waveform(withMeters) == StatusLine(tone: .waiting, text: Self.problem))
        let withoutMeters = context(settings: OverlaySettings(waveformMode: .hideBoth, isPhaseMeterEnabled: false), captureProblem: Self.problem)
        #expect(StatusText.waveform(withoutMeters) == StatusLine(tone: .live, text: "Live: both waveforms hidden"))
    }

    @Test func featuresThatNeedNoPixelsStayLiveDuringACaptureProblem() {
        let cover = context(settings: OverlaySettings(bpmLimit: .coverFully), captureProblem: Self.problem)
        #expect(StatusText.bpmPrecision(cover) == StatusLine(tone: .live, text: "Live: jog BPM covered"))
        let normal = context(settings: OverlaySettings(waveformMode: .normal), captureProblem: Self.problem)
        #expect(StatusText.waveform(normal) == StatusLine(tone: .off, text: "Normal: rekordbox waveform unchanged"))
    }

    @Test func aMissingScreenRecordingGrantIsReportedBeforeACaptureProblem() {
        let line = StatusText.phaseMeter(context(screenRecording: false, captureProblem: Self.problem))
        #expect(line == StatusLine(tone: .waiting, text: "Allow Screen Recording to read the beat grids"))
    }

    @Test func aCaptureProblemDoesNotOverrideFeaturesThatAreOff() {
        let meterOff = context(settings: OverlaySettings(isPhaseMeterEnabled: false), captureProblem: Self.problem)
        #expect(StatusText.phaseMeter(meterOff) == StatusLine(tone: .off, text: "Phase meter off"))
        let earmatchOff = context(settings: OverlaySettings(isEnabled: false), captureProblem: Self.problem)
        #expect(StatusText.waveform(earmatchOff) == StatusLine(tone: .off, text: "Earmatch is off"))
    }

    @Test func aBlankCaptureProblemCountsAsNone() {
        let line = StatusText.phaseMeter(context(settings: OverlaySettings(phaseMeterType: .box), captureProblem: "  "))
        #expect(line == StatusLine(tone: .live, text: "Live: Box (CDJ-2000NXS / XDJ-1000)"))
    }

    @Test func bpmSampleSplitsVisibleAndCoveredDigitsLikeTheJogMask() {
        #expect(BPMSample(bpm: 127.22, limit: .oneDecimal) == BPMSample(visible: "127.2", covered: "2"))
        #expect(BPMSample(bpm: 127.22, limit: .noDecimals) == BPMSample(visible: "127", covered: ".22"))
        #expect(BPMSample(bpm: 127.22, limit: .coverFully) == BPMSample(visible: "", covered: "127.22"))
        #expect(BPMSample(bpm: 127.22, limit: nil) == BPMSample(visible: "127.22", covered: ""))
    }
}
