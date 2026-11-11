import Testing
import EarmatchCore

@Suite struct StatusTextDiagnosticsTests {
    func report(rekordboxVersion: String? = "7.2.19.0342", extra: String = "") -> DiagnosticsReport {
        DiagnosticsReport(
            appVersion: "0.1.0 (1)",
            macOS: "Version 27.2 (Build 25C56)",
            hardware: "Mac14,2",
            architecture: "arm64",
            rekordboxVersion: rekordboxVersion,
            isAccessibilityGranted: true,
            isScreenRecordingGranted: false,
            activation: .rekordboxNotFrontmost,
            settings: OverlaySettings(isBPMLimitEnabled: true, bpmLimit: .noDecimals, waveformMode: .hideBoth,
                                      isPhaseMeterEnabled: false, phaseMeterType: .box),
            extra: extra
        )
    }

    @Test func listsVersionsPermissionsStatusAndSettingsOnePerLine() {
        #expect(report().text == """
        Earmatch diagnostics
        Earmatch: 0.1.0 (1)
        macOS: Version 27.2 (Build 25C56)
        Mac: Mac14,2 (arm64)
        rekordbox: 7.2.19.0342
        Accessibility: allowed
        Screen Recording: missing
        Status: Paused until rekordbox is in front
        Enabled: on
        Limit BPM display: on, No decimals
        Waveform: Hide Both
        Phase meter: off, Box (CDJ-2000NXS / XDJ-1000)
        Counter: Bar position (rekordbox)
        Full blind mode: off
        Booth: custom
        Mix trainer: on

        """)
    }

    @Test func namesTheBoothPresetInUse() {
        var settings = OverlaySettings()
        BoothPreset.cdj3000.apply(to: &settings)
        settings.isBlindModeEnabled = true
        var report = report()
        report.settings = settings

        #expect(report.text.contains("\nCounter: Countdown to next cue (players)\nFull blind mode: on\nBooth: CDJ-3000\n"))
    }

    @Test func saysWhenRekordboxIsNotRunning() {
        #expect(report(rekordboxVersion: nil).text.contains("\nrekordbox: not running\n"))
    }

    @Test func appendsExtraDiagnosticsAfterABlankLine() {
        let text = report(extra: "Capture: 120 fps\n").text
        #expect(text.hasSuffix("Mix trainer: on\n\nCapture: 120 fps\n"))
    }

    @Test func optionNamesMatchTheWindowLabels() {
        #expect(BPMLimit.allCases.map(\.displayName) == ["One decimal", "No decimals", "Cover fully"])
        #expect(WaveformMode.allCases.map(\.displayName) == ["Normal", "Split", "Hide Both"])
        #expect(PhaseMeterType.allCases.map(\.displayName) == ["Box", "Line", "3000 Meter", "3000 Wave"])
        #expect(PhaseMeterType.allCases.map(\.title) == [
            "Box (CDJ-2000NXS / XDJ-1000)", "Line (CDJ-2000NXS2 Type 2)", "CDJ-3000 Phase meter", "CDJ-3000 Waveform",
        ])
        #expect(CounterStyle.allCases.map(\.displayName) == ["Cue countdown", "Bar position"])
    }
}
