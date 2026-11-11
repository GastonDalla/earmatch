import Testing
import EarmatchCore

@Suite struct StatusTextLiveTests {
    func context(
        _ activation: OverlayActivation = .active,
        settings: OverlaySettings = OverlaySettings(),
        screenRecording: Bool = true,
        captureProblem: String? = nil
    ) -> StatusContext {
        StatusContext(activation: activation, settings: settings, isScreenRecordingGranted: screenRecording,
                      rekordboxVersion: "7.2.19.0342", captureProblem: captureProblem)
    }

    let twoDecks = LiveDecksSnapshot(
        decks: [
            LiveDeckSnapshot(number: 1, title: "Track A", isMaster: true, meter: nil),
            LiveDeckSnapshot(number: 2, title: "Track B", isMaster: false, meter: nil),
        ],
        framesPerSecond: 112.4,
        latencyMilliseconds: 2.06
    )

    @Test func showsFrameRateAndLatencyWhileTheDecksAreMirrored() {
        #expect(StatusText.liveDecks(context(), snapshot: twoDecks) == StatusLine(tone: .live, text: "Live · 112 fps · 2.1 ms"))
    }

    @Test func leavesOutMeasurementsThePipelineHasNotMadeYet() {
        var unmeasured = twoDecks
        unmeasured.framesPerSecond = 0
        unmeasured.latencyMilliseconds = 0
        #expect(StatusText.liveDecks(context(), snapshot: unmeasured) == StatusLine(tone: .live, text: "Live"))
    }

    @Test func isOffWhileEarmatchIsOff() {
        let line = StatusText.liveDecks(context(settings: OverlaySettings(isEnabled: false)), snapshot: twoDecks)
        #expect(line == StatusLine(tone: .off, text: "Earmatch is off"))
    }

    @Test func saysWhatItIsWaitingForWhileRekordboxIsNotReady() {
        let line = StatusText.liveDecks(context(.rekordboxNotFrontmost), snapshot: twoDecks)
        #expect(line == StatusLine(tone: .waiting, text: "Paused until rekordbox is in front"))
    }

    @Test func needsScreenRecording() {
        let line = StatusText.liveDecks(context(screenRecording: false), snapshot: twoDecks)
        #expect(line == StatusLine(tone: .waiting, text: "Allow Screen Recording to mirror the decks"))
    }

    @Test func reportsACaptureProblem() {
        let line = StatusText.liveDecks(context(captureProblem: "rekordbox window not found"), snapshot: twoDecks)
        #expect(line == StatusLine(tone: .waiting, text: "rekordbox window not found"))
    }

    @Test func waitsForTheFirstSnapshotWithDecks() {
        #expect(StatusText.liveDecks(context(), snapshot: nil) == StatusLine(tone: .waiting, text: "Reading the decks…"))
        #expect(StatusText.liveDecks(context(), snapshot: .empty) == StatusLine(tone: .waiting, text: "Reading the decks…"))
    }

    @Test func theToneOnlyCheckAgreesWithTheLine() {
        for activation in LocalizationTests.activations {
            for settings in LocalizationTests.settingsVariants {
                for screenRecording in [true, false] {
                    for problem in [nil, "", "frame too small"] {
                        let context = StatusContext(activation: activation, settings: settings, isScreenRecordingGranted: screenRecording,
                                                    rekordboxVersion: nil, captureProblem: problem)
                        for snapshot in LocalizationTests.snapshots {
                            #expect(StatusText.isMirroringDecks(context, snapshot: snapshot)
                                == (StatusText.liveDecks(context, snapshot: snapshot).tone == .live))
                        }
                    }
                }
            }
        }
    }
}
