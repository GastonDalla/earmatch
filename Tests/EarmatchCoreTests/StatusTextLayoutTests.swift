import Testing
import EarmatchCore

@Suite struct StatusTextLayoutTests {
    func context(_ layout: String?, settings: OverlaySettings = OverlaySettings(), activation: OverlayActivation = .active,
                 version: String? = "7.2.19.0342") -> StatusContext {
        StatusContext(activation: activation, settings: settings, isScreenRecordingGranted: true, rekordboxVersion: version,
                      captureProblem: nil, layoutName: layout)
    }

    @Test func theHeaderNamesTheLayout() {
        #expect(StatusText.connection(context("4Deck Vertical")) == StatusLine(tone: .live, text: "rekordbox 7.2.19.0342 connected · 4Deck Vertical"))
        #expect(StatusText.connection(context("2Deck Vertical", version: nil)) == StatusLine(tone: .live, text: "rekordbox connected · 2Deck Vertical"))
        #expect(StatusText.connection(context("Browse", activation: .wrongLayout("Browse")))
            == StatusLine(tone: .live, text: "rekordbox 7.2.19.0342 connected · Browse"))
    }

    @Test func horizontalLayoutsSplitTheirDecks() {
        let split = OverlaySettings(waveformMode: .split)
        #expect(StatusText.waveform(context("2Deck Horizontal", settings: split))
            == StatusLine(tone: .live, text: "Live in 2Deck Horizontal: decks split, one playhead each"))
        #expect(StatusText.waveform(context("4Deck Horizontal", settings: split))
            == StatusLine(tone: .live, text: "Live in 4Deck Horizontal: decks split, one playhead each"))
    }

    @Test func verticalLayoutsKeepEachDeckInItsOwnColumn() {
        let split = OverlaySettings(waveformMode: .split)
        #expect(StatusText.waveform(context("2Deck Vertical", settings: split))
            == StatusLine(tone: .live, text: "Live in 2Deck Vertical: each deck keeps its own column"))
    }

    @Test func hideBothHidesEveryWaveform() {
        let hide = OverlaySettings(waveformMode: .hideBoth)
        #expect(StatusText.waveform(context("4Deck Vertical", settings: hide)) == StatusLine(tone: .live, text: "Live in 4Deck Vertical: waveforms hidden"))
    }

    @Test func theMeterLineNamesTheLayout() {
        let line = OverlaySettings(waveformMode: .split, phaseMeterType: .line)
        #expect(StatusText.phaseMeter(context("4Deck Vertical", settings: line))
            == StatusLine(tone: .live, text: "Live in 4Deck Vertical: Line (CDJ-2000NXS2 Type 2)"))
        let wave = OverlaySettings(waveformMode: .split, phaseMeterType: .cdj3000Waveform)
        #expect(StatusText.phaseMeter(context("4Deck Horizontal", settings: wave))
            == StatusLine(tone: .live, text: "Live in 4Deck Horizontal: CDJ-3000 Waveform"))
        #expect(StatusText.phaseMeter(context("2Deck Vertical", settings: wave))
            == StatusLine(tone: .live, text: "Live in 2Deck Vertical: CDJ-3000 Waveform · master waveform in horizontal Split only"))
        let normal = OverlaySettings(waveformMode: .normal, phaseMeterType: .cdj3000Waveform)
        #expect(StatusText.phaseMeter(context("2Deck Horizontal", settings: normal))
            == StatusLine(tone: .live, text: "Live in 2Deck Horizontal: CDJ-3000 Waveform · master waveform in horizontal Split only"))
    }

    @Test func everyLayoutLineGoesThroughTheLookup() {
        let marker: L10n.Lookup = { "⟦\($0)⟧" }
        var lines: [StatusLine] = []
        L10n.$override.withValue(marker) {
            for name in DeckArrangement.allCases.map(\.rawValue) {
                for mode in WaveformMode.allCases {
                    for type in PhaseMeterType.allCases {
                        let context = context(name, settings: OverlaySettings(waveformMode: mode, phaseMeterType: type))
                        lines += [StatusText.connection(context), StatusText.waveform(context), StatusText.phaseMeter(context)]
                    }
                }
            }
        }
        #expect(lines.allSatisfy { $0.text.hasPrefix("⟦") }, "\(lines.filter { !$0.text.hasPrefix("⟦") })")
    }
}
