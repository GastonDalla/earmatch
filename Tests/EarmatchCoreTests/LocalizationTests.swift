import Foundation
import Testing
import EarmatchCore

@Suite struct LocalizationTests {
    static let marker: L10n.Lookup = { "⟦\($0)⟧" }

    static func marked<T>(_ body: () -> T) -> T {
        L10n.$override.withValue(marker, operation: body)
    }

    static func unmarked(_ text: String) -> String {
        var depth = 0
        var rest = ""
        for character in text {
            switch character {
            case "⟦": depth += 1
            case "⟧": depth -= 1
            default: if depth == 0 { rest.append(character) }
            }
        }
        return rest
    }

    static func isLocalized(_ text: String) -> Bool {
        !text.isEmpty && unmarked(text).rangeOfCharacter(from: .letters) == nil
    }

    static let activations: [OverlayActivation] = [
        .accessibilityMissing, .screenRecordingMissing, .rekordboxNotRunning, .mainWindowNotFound, .rekordboxNotFrontmost,
        .wrongMode(.export), .wrongLayout("Browse"), .wrongLayout("4Deck Horizontal"), .wrongLayout(nil), .decksNotFound, .active,
    ]

    static var settingsVariants: [OverlaySettings] {
        var variants: [OverlaySettings] = []
        for enabled in [true, false] {
            for limit in BPMLimit.allCases {
                for blind in [true, false] {
                    for limitOn in [true, false] {
                        variants.append(OverlaySettings(isEnabled: enabled, isBPMLimitEnabled: limitOn, bpmLimit: limit, isBlindModeEnabled: blind))
                    }
                }
            }
            for mode in WaveformMode.allCases {
                for meter in PhaseMeterType.allCases {
                    for meterOn in [true, false] {
                        variants.append(OverlaySettings(isEnabled: enabled, waveformMode: mode, isPhaseMeterEnabled: meterOn, phaseMeterType: meter))
                    }
                }
            }
        }
        return variants
    }

    static let snapshots: [LiveDecksSnapshot?] = [
        nil,
        LiveDecksSnapshot(decks: [], framesPerSecond: 0, latencyMilliseconds: 0),
        LiveDecksSnapshot(decks: [LiveDeckSnapshot(number: 1, title: nil, isMaster: false, meter: nil)], framesPerSecond: 0, latencyMilliseconds: 0),
        LiveDecksSnapshot(decks: [LiveDeckSnapshot(number: 1, title: nil, isMaster: false, meter: nil)], framesPerSecond: 112, latencyMilliseconds: 0),
        LiveDecksSnapshot(decks: [LiveDeckSnapshot(number: 1, title: nil, isMaster: false, meter: nil)], framesPerSecond: 0, latencyMilliseconds: 2.1),
        LiveDecksSnapshot(decks: [LiveDeckSnapshot(number: 1, title: nil, isMaster: false, meter: nil)], framesPerSecond: 112, latencyMilliseconds: 2.1),
    ]

    @Test func everyStatusLineGoesThroughTheLookup() {
        var unlocalized: Set<String> = []
        Self.marked {
            for activation in Self.activations {
                for settings in Self.settingsVariants {
                    for screenRecording in [true, false] {
                        for version in ["7.2.19.0342", nil] {
                            let context = StatusContext(activation: activation, settings: settings, isScreenRecordingGranted: screenRecording,
                                                        rekordboxVersion: version, captureProblem: nil)
                            var lines = [StatusText.connection(context), StatusText.bpmPrecision(context),
                                         StatusText.waveform(context), StatusText.phaseMeter(context)]
                            lines += Self.snapshots.map { StatusText.liveDecks(context, snapshot: $0) }
                            for line in lines where !Self.isLocalized(line.text) { unlocalized.insert(line.text) }
                        }
                    }
                }
            }
        }
        #expect(unlocalized.isEmpty, "not localized: \(unlocalized.sorted())")
    }

    @Test func aCaptureProblemFromTheAppIsShownAsItCame() {
        let context = StatusContext(activation: .active, settings: OverlaySettings(), isScreenRecordingGranted: true,
                                    rekordboxVersion: nil, captureProblem: "⟦Capture failed⟧")
        let line = Self.marked { StatusText.waveform(context) }
        #expect(line == StatusLine(tone: .waiting, text: "⟦Capture failed⟧"))
    }

    @Test func everyActivationMessageGoesThroughTheLookup() {
        let messages = Self.marked { Self.activations.map(\.message) }
        #expect(messages.allSatisfy(Self.isLocalized), "\(messages)")
    }

    @Test func everyOptionNameAndGuideGoesThroughTheLookup() {
        let texts: [String] = Self.marked {
            BPMLimit.allCases.flatMap { [$0.displayName, $0.explanation] }
                + WaveformMode.allCases.flatMap { [$0.displayName, $0.explanation] }
                + PhaseMeterType.allCases.flatMap { [$0.displayName, $0.title, $0.explanation] }
                + CounterStyle.allCases.flatMap { [$0.displayName, $0.title, $0.explanation] }
                + BoothPreset.allCases.map(\.explanation)
                + [BoothPreset.customExplanation]
                + [OptionText.boothTitle, OptionText.boothSectionTitle, OptionText.boothSubtitle, OptionText.counterTitle,
                   OptionText.blindModeTitle, OptionText.blindModeSubtitle, OptionText.blindModeMenuTitle,
                   OptionText.counterMenuTitle, OptionText.boothMenuTitle, OptionText.phaseMeterStyleMenuTitle,
                   OptionText.mixTrainerTitle, OptionText.mixTrainerSubtitle, OptionText.mixTrainerMenuTitle,
                   OptionText.noOtherMaster]
        }
        #expect(texts.allSatisfy(Self.isLocalized), "not localized: \(texts.filter { !Self.isLocalized($0) })")
    }

    @Test func hardwareLabelsStayInEnglishAsThePlayersPrintThem() {
        let display = Self.marked {
            PhaseMeterModel.display(for: 1, decks: [
                PhaseMeterDeckInput(number: 1, phaseInBar: 1.5, counter: BarPosition(bar: 24, beat: 2)),
                PhaseMeterDeckInput(number: 2, phaseInBar: 2.5, counter: nil),
            ], master: 2, style: .box)
        }
        let names = Self.marked { BoothPreset.allCases.map(\.displayName) }

        #expect(Self.marked { PhaseMeterDisplay.counterUnit } == "Bars")
        #expect(Self.marked { PhaseMeterDisplay.emptyCounter } == "--.-")
        #expect(display.label?.lines == ["MASTER", "PLAYER"])
        #expect(display.rows.map(\.counter) == ["--.-", "24.2"])
        #expect(names == ["CDJ-3000", "CDJ-2000NXS2", "XDJ-1000"])
    }

    @Test func withoutTranslationsTheEnglishKeyIsTheText() {
        #expect(L10n.text("Earmatch is off") == "Earmatch is off")
        #expect(L10n.format("Live · %.0f fps", 112.4) == "Live · 112 fps")
    }

    @Test func numbersInFormatsAreNotTranslated() {
        let text = Self.marked { L10n.format("Live · %.0f fps · %.1f ms", 112.0, 2.14) }
        #expect(text == "⟦Live · 112 fps · 2.1 ms⟧")
    }

    @Test func theInstalledLanguageAnswersEveryThreadUntilTheNextInstall() async {
        await #expect(processExitsWith: .success) {
            let spanish: L10n.Lookup = { $0 == "Capture stopped" ? "Captura detenida" : $0 }
            L10n.install(spanish, locale: Locale(identifier: "es_ES"))
            let answers = await withTaskGroup(of: [String].self) { group in
                for _ in 0 ..< 8 {
                    group.addTask { [L10n.text("Capture stopped"), L10n.format("%.1f ms", 2.5), L10n.locale?.identifier ?? "-"] }
                }
                return await group.reduce(into: []) { $0.append($1) }
            }
            precondition(answers.count == 8 && answers.allSatisfy { $0 == ["Captura detenida", "2,5 ms", "es_ES"] })
            precondition(L10n.english { [L10n.text("Capture stopped"), L10n.format("%.1f ms", 2.5)] } == ["Capture stopped", "2.5 ms"])
            L10n.install({ $0 }, locale: Locale(identifier: "en_US"))
            precondition(L10n.text("Capture stopped") == "Capture stopped" && L10n.locale?.identifier == "en_US")
        }
    }
}
