import Foundation
import Testing
import EarmatchCore

@Test func defaultSettingsMatchTheUsersClubModeSetup() {
    let settings = OverlaySettings()
    #expect(settings.isEnabled)
    #expect(settings.isBPMLimitEnabled)
    #expect(settings.bpmLimit == .oneDecimal)
    #expect(settings.waveformMode == .split)
    #expect(settings.isPhaseMeterEnabled)
    #expect(settings.phaseMeterType == .line)
}

@Test func settingsSurviveAJSONRoundTrip() throws {
    let original = OverlaySettings(isEnabled: false, isBPMLimitEnabled: false, bpmLimit: .coverFully, waveformMode: .hideBoth, isPhaseMeterEnabled: false, phaseMeterType: .box)
    let decoded = try JSONDecoder().decode(OverlaySettings.self, from: JSONEncoder().encode(original))
    #expect(decoded == original)
}

@Test func settingsSavedByAnOlderVersionKeepTheirValuesAndDefaultTheRest() throws {
    let decoded = try JSONDecoder().decode(OverlaySettings.self, from: Data(#"{"isEnabled":false,"bpmLimit":"noDecimals"}"#.utf8))
    #expect(!decoded.isEnabled)
    #expect(decoded.bpmLimit == .noDecimals)
    #expect(decoded.waveformMode == OverlaySettings().waveformMode)
    #expect(decoded.phaseMeterType == OverlaySettings().phaseMeterType)
}

@Test func anOptionFromANewerVersionFallsBackOnlyForThatSetting() throws {
    let decoded = try JSONDecoder().decode(OverlaySettings.self, from: Data(#"{"isEnabled":false,"bpmLimit":"twoDecimalsBlinking"}"#.utf8))
    #expect(!decoded.isEnabled)
    #expect(decoded.bpmLimit == OverlaySettings().bpmLimit)
}

@Test func meterStylesSavedBeforeTheCDJ3000StylesStillDecode() throws {
    let box = try JSONDecoder().decode(OverlaySettings.self, from: Data(#"{"phaseMeterType":"box"}"#.utf8))
    let line = try JSONDecoder().decode(OverlaySettings.self, from: Data(#"{"phaseMeterType":"line"}"#.utf8))
    #expect(box.phaseMeterType == .box)
    #expect(line.phaseMeterType == .line)
}

@Test func everyMeterStyleIsOffered() {
    #expect(PhaseMeterType.allCases == [.box, .line, .cdj3000PhaseMeter, .cdj3000Waveform])
}

@Test func newOptionsDefaultToTheBehaviourBeforeThem() {
    let settings = OverlaySettings()
    #expect(settings.counterStyle == .barPosition)
    #expect(!settings.isBlindModeEnabled)
    #expect(settings.boothPreset == nil)
}

@Test func newOptionsSurviveAJSONRoundTrip() throws {
    var original = OverlaySettings(phaseMeterType: .cdj3000Waveform)
    original.counterStyle = .cueCountdown
    original.isBlindModeEnabled = true
    original.boothPreset = .cdj3000
    let decoded = try JSONDecoder().decode(OverlaySettings.self, from: JSONEncoder().encode(original))
    #expect(decoded == original)
}

@Test func settingsSavedWithoutTheNewOptionsKeepTheirDefaults() throws {
    let decoded = try JSONDecoder().decode(OverlaySettings.self, from: Data(#"{"phaseMeterType":"line","counterStyle":"sideways"}"#.utf8))
    #expect(decoded.phaseMeterType == .line)
    #expect(decoded.counterStyle == .barPosition)
    #expect(!decoded.isBlindModeEnabled)
    #expect(decoded.boothPreset == nil)
}

@Test func theMixTrainerIsOnByDefaultAndWhenSavedSettingsLackIt() throws {
    #expect(OverlaySettings().isMixTrainerEnabled)
    let decoded = try JSONDecoder().decode(OverlaySettings.self, from: Data(#"{"isEnabled":true,"phaseMeterType":"box"}"#.utf8))
    #expect(decoded.isMixTrainerEnabled)
}

@Test func theMixTrainerSwitchSurvivesAJSONRoundTrip() throws {
    var original = OverlaySettings()
    original.isMixTrainerEnabled = false
    let decoded = try JSONDecoder().decode(OverlaySettings.self, from: JSONEncoder().encode(original))
    #expect(!decoded.isMixTrainerEnabled)
    #expect(decoded == original)
}
