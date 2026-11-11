import Testing
import EarmatchCore

@Suite struct BoothPresetTests {
    @Test func theCDJ3000BoothUsesThePlayersDefaultWaveformMeter() {
        var settings = OverlaySettings(isBPMLimitEnabled: false, bpmLimit: .coverFully, waveformMode: .normal,
                                       isPhaseMeterEnabled: false, phaseMeterType: .box)
        BoothPreset.cdj3000.apply(to: &settings)

        #expect(settings.isPhaseMeterEnabled)
        #expect(settings.phaseMeterType == .cdj3000Waveform)
        #expect(settings.counterStyle == .cueCountdown)
        #expect(settings.isBPMLimitEnabled)
        #expect(settings.bpmLimit == .oneDecimal)
        #expect(settings.waveformMode == .split)
        #expect(settings.boothPreset == .cdj3000)
    }

    @Test func theNXS2AndXDJ1000BoothsUseTheBoxMeter() {
        for preset in [BoothPreset.cdj2000NXS2, .xdj1000] {
            var settings = OverlaySettings(bpmLimit: .noDecimals, waveformMode: .hideBoth, phaseMeterType: .cdj3000PhaseMeter)
            preset.apply(to: &settings)

            #expect(settings.phaseMeterType == .box)
            #expect(settings.counterStyle == .cueCountdown)
            #expect(settings.bpmLimit == .oneDecimal)
            #expect(settings.waveformMode == .split)
            #expect(settings.boothPreset == preset)
        }
    }

    @Test func applyingAPresetLeavesEarmatchsOtherSettingsAlone() {
        var settings = OverlaySettings(isEnabled: false)
        settings.isBlindModeEnabled = true
        BoothPreset.xdj1000.apply(to: &settings)

        #expect(!settings.isEnabled)
        #expect(settings.isBlindModeEnabled)
    }

    @Test func thePresetShowsAsSelectedWhileTheSettingsMatchIt() {
        var settings = OverlaySettings()
        BoothPreset.cdj3000.apply(to: &settings)
        #expect(BoothPreset.selected(in: settings) == .cdj3000)

        settings.waveformMode = .normal
        #expect(BoothPreset.selected(in: settings) == nil)

        settings.waveformMode = .split
        #expect(BoothPreset.selected(in: settings) == .cdj3000)
    }

    @Test func boothsWithTheSameSettingsShowTheOneThatWasChosen() {
        var settings = OverlaySettings()
        BoothPreset.xdj1000.apply(to: &settings)
        #expect(BoothPreset.selected(in: settings) == .xdj1000)

        BoothPreset.cdj2000NXS2.apply(to: &settings)
        #expect(BoothPreset.selected(in: settings) == .cdj2000NXS2)
    }

    @Test func settingsMadeByHandSelectTheOnlyBoothTheyMatch() {
        var settings = OverlaySettings(bpmLimit: .oneDecimal, waveformMode: .split, phaseMeterType: .cdj3000Waveform)
        settings.counterStyle = .cueCountdown
        #expect(BoothPreset.selected(in: settings) == .cdj3000)

        settings.phaseMeterType = .box
        #expect(BoothPreset.selected(in: settings) == nil)
    }

    @Test func aBoothNeedsTheMeterAndTheLimitSwitchedOn() {
        var settings = OverlaySettings()
        BoothPreset.cdj3000.apply(to: &settings)
        settings.isPhaseMeterEnabled = false
        #expect(BoothPreset.selected(in: settings) == nil)

        settings.isPhaseMeterEnabled = true
        settings.isBPMLimitEnabled = false
        #expect(BoothPreset.selected(in: settings) == nil)
    }
}
