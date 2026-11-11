import Foundation
import Testing
import EarmatchCore

@Suite struct DiagnosticsEnglishTests {
    static let spanish: L10n.Lookup = { key in
        [
            "granted": "concedido", "missing": "falta", "allowed": "permitido", "on": "activado", "off": "desactivado",
            "loaded": "cargado", "empty": "vacío", "yes": "sí", "no": "no", "not running": "no se está ejecutando",
            "custom": "personalizado", "Earmatch diagnostics": "Diagnóstico de Earmatch",
            "Paused until rekordbox is in front": "En pausa hasta que rekordbox esté delante",
            "No decimals": "Sin decimales", "Hide Both": "Ocultar ambas",
            "Box (CDJ-2000NXS / XDJ-1000)": "Cajas (CDJ-2000NXS / XDJ-1000)",
            "Bar position (rekordbox)": "Posición en el compás (rekordbox)",
        ][key] ?? "⟦\(key)⟧"
    }

    static func inSpanish<T>(_ body: () -> T) -> T {
        L10n.$override.withValue(spanish) { L10n.$overrideLocale.withValue(Locale(identifier: "es_ES"), operation: body) }
    }

    static let report = DiagnosticsReport(
        appVersion: "0.1.0 (1)", macOS: "Version 27.2 (Build 25C56)", hardware: "Mac14,2", architecture: "arm64",
        rekordboxVersion: nil, isAccessibilityGranted: true, isScreenRecordingGranted: false, activation: .rekordboxNotFrontmost,
        settings: OverlaySettings(isBPMLimitEnabled: true, bpmLimit: .noDecimals, waveformMode: .hideBoth, isPhaseMeterEnabled: false, phaseMeterType: .box),
        extra: ""
    )

    static let capture = CaptureDiagnostics(
        earmatchVersion: "0.1.0", macOSVersion: "27.2.0", rekordboxVersion: "7.2.19", accessibilityGranted: true,
        screenRecordingGranted: false, activation: "Active on rekordbox", layout: "4Deck Vertical", capture: "running",
        mappingProblem: nil, waveformFound: true, framesPerSecond: 110.44, captureJitterMilliseconds: 0.75,
        analysisMilliseconds: 0.61, maxAnalysisMilliseconds: 2.14, earliestLatencyMilliseconds: 5.8, latencyMilliseconds: 14.06,
        maxLatencyMilliseconds: 18.2, refreshesPerSecond: 118, meterErrorMilliseconds: 2.1,
        decks: [
            .init(number: 1, isLoaded: true, reading: "live", beatSpacing: 6.02, phaseInBar: 2.5, beatsPerSecond: 2.25,
                  isMaster: false, headerBPM: 135, elapsedSeconds: 327.1),
            .init(number: 3, isLoaded: false, reading: "-", beatSpacing: nil, phaseInBar: nil, beatsPerSecond: nil,
                  isMaster: true, headerBPM: nil, elapsedSeconds: nil),
        ]
    )

    @Test func theSettingsReportStaysEnglishWhateverTheAppLanguage() {
        let english = Self.report.text
        let spanish = Self.inSpanish { Self.report.text }

        #expect(spanish == english)
        #expect(spanish.contains("\nAccessibility: allowed\nScreen Recording: missing\n"))
        #expect(spanish.contains("\nStatus: Paused until rekordbox is in front\n"))
        #expect(spanish.contains("\nrekordbox: not running\n"))
        #expect(spanish.contains("\nLimit BPM display: on, No decimals\nWaveform: Hide Both\n"))
    }

    @Test func theCaptureReportStaysEnglishWithPOSIXNumbers() {
        let text = Self.inSpanish { Self.capture.text }

        #expect(text == """
        Earmatch 0.1.0, macOS 27.2.0, rekordbox 7.2.19
        Accessibility: granted; Screen Recording: missing
        Activation: Active on rekordbox
        Layout: 4Deck Vertical
        Capture: running
        Stacked waveform: found
        Frames: 110.4 fps (interval sd 0.75 ms), analysis 0.61 ms avg / 2.14 ms max
        Latency: capture to screen 5.8 to 14.1 ms avg, 18.2 ms max; meter drawn 118 times/s, predicted for the screen (2.1 ms from rekordbox)
        Deck 1: loaded, grid live, spacing 6.02 pt/beat, phase 2.50, playing 135.0 BPM, master no, BPM 135.00, elapsed 327.1 s
        Deck 3: empty, grid -, spacing -, phase -, playing -, master yes, BPM -, elapsed -
        """)
    }

    @Test func aMappingProblemFollowsTheCaptureState() {
        var capture = Self.capture
        capture.mappingProblem = "frame shows 800x600 pt but the window is 1492x866 pt"
        capture.layout = nil
        capture.meterErrorMilliseconds = nil

        let lines = capture.text.split(separator: "\n").map(String.init)

        #expect(lines.contains("Capture: running (frame shows 800x600 pt but the window is 1492x866 pt)"))
        #expect(lines.contains("Layout: -"))
        #expect(lines.contains("Latency: capture to screen 5.8 to 14.1 ms avg, 18.2 ms max; meter drawn 118 times/s, predicted for the screen"))
    }
}
