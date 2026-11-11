import EarmatchCore
import Foundation

enum WindowText {
    static var permissions: String { L10n.text("PERMISSIONS") }
    static var liveDecks: String { L10n.text("LIVE DECKS") }
    static var bpmPrecisionSection: String { L10n.text("BPM PRECISION") }
    static var waveformSection: String { L10n.text("WAVEFORM") }
    static var phaseMeterSection: String { L10n.text("PHASE METER") }
    static var hotkeysSection: String { L10n.text("HOTKEYS") }
    static var languageSection: String { L10n.text("LANGUAGE") }
    static var language: String { L10n.text("Language") }
    static var enable: String { L10n.text("Enable Earmatch") }
    static var enableHint: String { L10n.text("Turns every overlay on rekordbox on or off. Shortcut: Control Option Command E.") }
    static var enableHelp: String { L10n.text("Turns every overlay on rekordbox on or off (⌃⌥⌘E)") }
    static var accessibility: String { L10n.text("Accessibility") }
    static var accessibilityReason: String { L10n.text("Finds rekordbox's decks, labels and window position.") }
    static var screenRecording: String { L10n.text("Screen Recording") }
    static var screenRecordingReason: String { L10n.text("Sees rekordbox's waveforms and digits; never saved.") }
    static var reopenHint: String { L10n.text("If macOS asks, quit and reopen Earmatch.") }
    static var restart: String { L10n.text("Restart Earmatch") }
    static var allowed: String { L10n.text("Allowed") }
    static var openSettings: String { L10n.text("Open Settings") }
    static var permissionMissing: String { L10n.text("A permission is missing, so Earmatch can't read rekordbox yet.") }
    static var openSetup: String { L10n.text("Open Setup") }
    static var limitBPM: String { L10n.text("Limit BPM display") }
    static var limitBPMSubtitle: String { L10n.text("Covers the jog BPM's digits, so you match by ear") }
    static var bpmPrecision: String { L10n.text("BPM precision") }
    static var sampleHidden: String { L10n.text("Sample: BPM hidden") }
    static var waveform: String { L10n.text("Waveform") }
    static var phaseMeter: String { L10n.text("Phase meter") }
    static var phaseMeterSubtitle: String { L10n.text("Pioneer-style beat meter on each deck") }
    static var phaseMeterType: String { L10n.text("Phase meter type") }
    static var rightOptionKey: String { L10n.text("Right ⌥") }
    static var peekHint: String { L10n.text("Hold to see rekordbox unchanged") }
    static var toggleHint: String { L10n.text("Turn Earmatch on or off") }
    static var switchSectionsHint: String { L10n.text("Switch between sections") }
    static var hotkeysSpoken: String { L10n.text("Hotkeys: hold Right Option to see rekordbox unchanged; Control Option Command E turns Earmatch on or off") }
    static var copyDiagnostics: String { L10n.text("Copy Diagnostics") }
    static var copyDiagnosticsHelp: String { L10n.text("Copy Diagnostics: copies a plain-text report (versions, permissions, status, settings) to attach to a bug report") }
    static var copied: String { L10n.text("Copied") }
    static var checkForUpdates: String { L10n.text("Check for Updates…") }
    static var quit: String { L10n.text("Quit Earmatch") }
    static var sections: String { L10n.text("Sections") }
    static var credit: (before: String, after: String) {
        let line = Credit.line
        guard let range = line.range(of: Credit.author) else { return (line + " ", "") }
        return (String(line[..<range.lowerBound]), String(line[range.upperBound...]))
    }
    static var creditHelp: String { L10n.format("Made by %@. Opens %@ in your browser", Credit.author, Credit.displayURL) }
    static var independence: String { L10n.text("Independent utility. Not affiliated with AlphaTheta, Pioneer DJ or rekordbox.") }

    private static var sampleFormat: String { L10n.template("Sample: BPM reads %@") }
    private static var statusFormat: String { L10n.template("Status: %@") }
    private static var shortcutFormat: String { L10n.template("Shortcut: Command %ld") }

    static func sample(reading visible: String) -> String {
        String(format: sampleFormat, visible)
    }

    static func status(_ title: String) -> String {
        String(format: statusFormat, title)
    }

    static func shortcut(_ number: Int) -> String {
        String(format: shortcutFormat, locale: AppLanguage.locale, number)
    }

    static func openSettingsToAllow(_ permission: String) -> String {
        L10n.format("Open Settings to allow %@", permission)
    }

    static func permissionSummary(_ permission: String, isGranted: Bool, reason: String) -> String {
        isGranted ? L10n.format("%@: allowed. %@", permission, reason) : L10n.format("%@: not allowed. %@", permission, reason)
    }

    static func counterHelp(_ style: CounterStyle) -> String {
        "\(style.title): \(style.explanation)"
    }
}
