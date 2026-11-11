import Foundation

public struct DiagnosticsReport: Equatable, Sendable {
    public var appVersion: String
    public var macOS: String
    public var hardware: String
    public var architecture: String
    public var rekordboxVersion: String?
    public var isAccessibilityGranted: Bool
    public var isScreenRecordingGranted: Bool
    public var activation: OverlayActivation
    public var settings: OverlaySettings
    public var extra: String

    public init(appVersion: String, macOS: String, hardware: String, architecture: String, rekordboxVersion: String?,
                isAccessibilityGranted: Bool, isScreenRecordingGranted: Bool, activation: OverlayActivation,
                settings: OverlaySettings, extra: String) {
        self.appVersion = appVersion
        self.macOS = macOS
        self.hardware = hardware
        self.architecture = architecture
        self.rekordboxVersion = rekordboxVersion
        self.isAccessibilityGranted = isAccessibilityGranted
        self.isScreenRecordingGranted = isScreenRecordingGranted
        self.activation = activation
        self.settings = settings
        self.extra = extra
    }

    public var text: String {
        L10n.english {
            func allowed(_ granted: Bool) -> String { granted ? "allowed" : "missing" }
            func onOff(_ on: Bool) -> String { on ? "on" : "off" }
            var lines = [
                "Earmatch diagnostics",
                "Earmatch: \(appVersion)",
                "macOS: \(macOS)",
                "Mac: \(hardware) (\(architecture))",
                "rekordbox: \(rekordboxVersion ?? "not running")",
                "Accessibility: \(allowed(isAccessibilityGranted))",
                "Screen Recording: \(allowed(isScreenRecordingGranted))",
                "Status: \(activation.message)",
                "Enabled: \(onOff(settings.isEnabled))",
                "Limit BPM display: \(onOff(settings.isBPMLimitEnabled)), \(settings.bpmLimit.displayName)",
                "Waveform: \(settings.waveformMode.displayName)",
                "Phase meter: \(onOff(settings.isPhaseMeterEnabled)), \(settings.phaseMeterType.title)",
                "Counter: \(settings.counterStyle.title)",
                "Full blind mode: \(onOff(settings.isBlindModeEnabled))",
                "Booth: \(BoothPreset.selected(in: settings)?.displayName ?? "custom")",
                "Mix trainer: \(onOff(settings.isMixTrainerEnabled))",
            ]
            let extra = extra.trimmingCharacters(in: .whitespacesAndNewlines)
            if !extra.isEmpty { lines += ["", extra] }
            return lines.joined(separator: "\n") + "\n"
        }
    }
}

extension BPMLimit {
    public var displayName: String {
        switch self {
        case .oneDecimal: L10n.text("One decimal")
        case .noDecimals: L10n.text("No decimals")
        case .coverFully: L10n.text("Cover fully")
        }
    }
}

extension WaveformMode {
    public var displayName: String {
        switch self {
        case .normal: L10n.text("Normal")
        case .split: L10n.text("Split")
        case .hideBoth: L10n.text("Hide Both")
        }
    }
}

extension PhaseMeterType {
    public var displayName: String {
        switch self {
        case .box: L10n.text("Box")
        case .line: L10n.text("Line")
        case .cdj3000PhaseMeter: L10n.text("3000 Meter")
        case .cdj3000Waveform: L10n.text("3000 Wave")
        }
    }

    public var title: String {
        switch self {
        case .box: L10n.text("Box (CDJ-2000NXS / XDJ-1000)")
        case .line: L10n.text("Line (CDJ-2000NXS2 Type 2)")
        case .cdj3000PhaseMeter: L10n.text("CDJ-3000 Phase meter")
        case .cdj3000Waveform: L10n.text("CDJ-3000 Waveform")
        }
    }
}

extension CounterStyle {
    public var displayName: String {
        switch self {
        case .cueCountdown: L10n.text("Cue countdown")
        case .barPosition: L10n.text("Bar position")
        }
    }

    public var title: String {
        switch self {
        case .cueCountdown: L10n.text("Countdown to next cue (players)")
        case .barPosition: L10n.text("Bar position (rekordbox)")
        }
    }
}
