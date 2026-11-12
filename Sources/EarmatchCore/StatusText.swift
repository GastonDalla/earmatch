import Foundation

public enum StatusTone: Equatable, Sendable {
    case live
    case waiting
    case off
}

public struct StatusLine: Equatable, Sendable {
    public var tone: StatusTone
    public var text: String

    public init(tone: StatusTone, text: String) {
        self.tone = tone
        self.text = text
    }
}

public struct StatusContext: Equatable, Sendable {
    public var activation: OverlayActivation
    public var settings: OverlaySettings
    public var isScreenRecordingGranted: Bool
    public var rekordboxVersion: String?
    public var captureProblem: String?
    public var layoutName: String?

    public init(activation: OverlayActivation, settings: OverlaySettings, isScreenRecordingGranted: Bool,
                rekordboxVersion: String?, captureProblem: String? = nil, layoutName: String? = nil) {
        self.activation = activation
        self.settings = settings
        self.isScreenRecordingGranted = isScreenRecordingGranted
        self.rekordboxVersion = rekordboxVersion
        self.captureProblem = captureProblem
        self.layoutName = layoutName
    }

    var arrangement: DeckArrangement? { layoutName.flatMap(DeckArrangement.init(rawValue:)) }
}

public enum StatusText {
    public static func connection(_ context: StatusContext) -> StatusLine {
        switch context.activation {
        case .accessibilityMissing, .screenRecordingMissing:
            StatusLine(tone: .waiting, text: context.activation.message)
        case .rekordboxNotRunning:
            StatusLine(tone: .waiting, text: context.activation.message)
        default:
            StatusLine(tone: .live, text: connectedText(version: context.rekordboxVersion, layout: context.layoutName))
        }
    }

    /// The line under the permissions screen's rows: waiting until both are allowed, then starting.
    public static func permissions(isWaiting: Bool) -> StatusLine {
        isWaiting
            ? StatusLine(tone: .waiting, text: L10n.text("Earmatch starts once both are allowed"))
            : StatusLine(tone: .live, text: L10n.text("Both allowed. Starting Earmatch…"))
    }

    private static func connectedText(version: String?, layout: String?) -> String {
        switch (version, layout) {
        case let (version?, layout?): L10n.format("rekordbox %@ connected · %@", version, layout)
        case let (version?, nil): L10n.format("rekordbox %@ connected", version)
        case let (nil, layout?): L10n.format("rekordbox connected · %@", layout)
        case (nil, nil): L10n.text("rekordbox connected")
        }
    }

    public static func bpmPrecision(_ context: StatusContext) -> StatusLine {
        let settings = context.settings
        let isOff = !settings.isBPMLimitEnabled && !settings.isBlindModeEnabled
        if let blocked = blocked(context, featureOff: isOff ? L10n.text("Off: rekordbox shows the full BPM") : nil) {
            return blocked
        }
        guard settings.isBPMLimitEnabled else {
            return StatusLine(tone: .live, text: L10n.text("Live: pitch and header BPM covered; jog BPM in full"))
        }
        return jogLimit(context, isBlind: settings.isBlindModeEnabled)
    }

    private static func jogLimit(_ context: StatusContext, isBlind: Bool) -> StatusLine {
        let settings = context.settings
        if !context.isScreenRecordingGranted && settings.bpmLimit != .coverFully {
            return StatusLine(tone: .waiting, text: L10n.text("No Screen Recording: jog BPM covered fully"))
        }
        if settings.bpmLimit != .coverFully, let problem = captureProblem(context) { return problem }
        let text = switch (settings.bpmLimit, isBlind) {
        case (.oneDecimal, false): L10n.text("Live: jog BPM shows one decimal")
        case (.noDecimals, false): L10n.text("Live: jog BPM shows no decimals")
        case (.coverFully, false): L10n.text("Live: jog BPM covered")
        case (.oneDecimal, true): L10n.text("Live: jog BPM shows one decimal; pitch and header BPM covered")
        case (.noDecimals, true): L10n.text("Live: jog BPM shows no decimals; pitch and header BPM covered")
        case (.coverFully, true): L10n.text("Live: jog BPM covered; pitch and header BPM covered")
        }
        return StatusLine(tone: .live, text: text)
    }

    public static func waveform(_ context: StatusContext) -> StatusLine {
        let mode = context.settings.waveformMode
        if let blocked = blocked(context, featureOff: mode == .normal ? L10n.text("Normal: rekordbox waveform unchanged") : nil) {
            return blocked
        }
        let layout = context.layoutName
        return switch mode {
        case .normal, .split:
            if context.arrangement?.orientation == .vertical {
                StatusLine(tone: .live, text: L10n.format("Live in %@: each deck keeps its own column", layout ?? ""))
            } else if !context.isScreenRecordingGranted {
                StatusLine(tone: .waiting, text: L10n.text("Allow Screen Recording to split the waveforms"))
            } else if let problem = captureProblem(context) {
                problem
            } else {
                StatusLine(tone: .live, text: layout.map { L10n.format("Live in %@: decks split, one playhead each", $0) }
                    ?? L10n.text("Live: decks split, one playhead each"))
            }
        case .hideBoth:
            if context.settings.isPhaseMeterEnabled, let problem = captureProblem(context) {
                problem
            } else {
                StatusLine(tone: .live, text: layout.map { L10n.format("Live in %@: waveforms hidden", $0) }
                    ?? L10n.text("Live: both waveforms hidden"))
            }
        }
    }

    public static func phaseMeter(_ context: StatusContext) -> StatusLine {
        let settings = context.settings
        if let blocked = blocked(context, featureOff: settings.isPhaseMeterEnabled ? nil : L10n.text("Phase meter off")) {
            return blocked
        }
        guard context.isScreenRecordingGranted else {
            return StatusLine(tone: .waiting, text: L10n.text("Allow Screen Recording to read the beat grids"))
        }
        if let problem = captureProblem(context) { return problem }
        let title = settings.phaseMeterType.title
        let showsMasterWaveform = settings.waveformMode == .split && context.arrangement?.orientation != .vertical
        guard let layout = context.layoutName else {
            if settings.phaseMeterType == .cdj3000Waveform && !showsMasterWaveform {
                return StatusLine(tone: .live, text: L10n.format("Live: %@ · master waveform in Split only", title))
            }
            return StatusLine(tone: .live, text: L10n.format("Live: %@", title))
        }
        if settings.phaseMeterType == .cdj3000Waveform && !showsMasterWaveform {
            return StatusLine(tone: .live, text: L10n.format("Live in %@: %@ · master waveform in horizontal Split only", layout, title))
        }
        return StatusLine(tone: .live, text: L10n.format("Live in %@: %@", layout, title))
    }

    static func blocked(_ context: StatusContext, featureOff: String?) -> StatusLine? {
        guard context.settings.isEnabled else { return StatusLine(tone: .off, text: L10n.text("Earmatch is off")) }
        if let featureOff { return StatusLine(tone: .off, text: featureOff) }
        guard context.activation.isActive else { return StatusLine(tone: .waiting, text: context.activation.message) }
        return nil
    }

    static func captureProblem(_ context: StatusContext) -> StatusLine? {
        guard let problem = context.captureProblem?.trimmingCharacters(in: .whitespacesAndNewlines), !problem.isEmpty else {
            return nil
        }
        return StatusLine(tone: .waiting, text: problem)
    }
}

public struct BPMSample: Equatable, Sendable {
    public var visible: String
    public var covered: String

    public init(visible: String, covered: String) {
        self.visible = visible
        self.covered = covered
    }

    public init(bpm: Double, limit: BPMLimit?) {
        let text = String(format: "%.2f", bpm)
        let visibleCount = switch limit {
        case nil: text.count
        case .oneDecimal: text.count - 1
        case .noDecimals: text.count - 3
        case .coverFully: 0
        }
        self.init(visible: String(text.prefix(visibleCount)), covered: String(text.dropFirst(visibleCount)))
    }
}
