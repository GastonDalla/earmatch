import Foundation

extension BPMLimit {
    public var explanation: String {
        switch self {
        case .oneDecimal: L10n.text("One decimal · 127.22 reads 127.2")
        case .noDecimals: L10n.text("No decimals · 127.22 reads 127")
        case .coverFully: L10n.text("Cover fully · the jog BPM is hidden")
        }
    }
}

extension WaveformMode {
    public var explanation: String {
        switch self {
        case .normal: L10n.text("Normal · rekordbox's stacked waveforms, unchanged")
        case .split: L10n.text("Split · each deck in its own half, like separate CDJ screens")
        case .hideBoth: L10n.text("Hide Both · no waveforms, only the phase meters")
        }
    }
}

extension PhaseMeterType {
    public var explanation: String {
        switch self {
        case .box: L10n.text("Box · CDJ-2000NXS / XDJ-1000 (NXS2 Type 1)")
        case .line: L10n.text("Line · CDJ-2000NXS2 / XDJ-1000MK2 Type 2")
        case .cdj3000PhaseMeter: L10n.text("CDJ-3000 · phase meter, counters on the left")
        case .cdj3000Waveform: L10n.text("CDJ-3000 · master waveform above yours (default)")
        }
    }
}

extension CounterStyle {
    public var explanation: String {
        switch self {
        case .cueCountdown: L10n.text("Bars and beats to the next cue ahead; --.- when none")
        case .barPosition: L10n.text("The bar and beat at the playhead, like rekordbox")
        }
    }
}

extension BoothPreset {
    public var displayName: String {
        switch self {
        case .cdj3000: "CDJ-3000"
        case .cdj2000NXS2: "CDJ-2000NXS2"
        case .xdj1000: "XDJ-1000"
        }
    }

    public var explanation: String {
        switch self {
        case .cdj3000: L10n.text("CDJ-3000 · waveform meter, cue countdown, one decimal")
        case .cdj2000NXS2: L10n.text("CDJ-2000NXS2 · box meter, cue countdown, one decimal")
        case .xdj1000: L10n.text("XDJ-1000 · box meter, cue countdown, one decimal")
        }
    }

    public static var customExplanation: String { L10n.text("Custom · your own mix of settings") }
}

public enum OptionText {
    public static var boothTitle: String { L10n.text("Booth") }
    public static var boothSectionTitle: String { L10n.text("BOOTH") }
    public static var boothSubtitle: String { L10n.text("Set everything like the players in the booth") }
    public static var counterTitle: String { L10n.text("Counter") }
    public static var blindModeTitle: String { L10n.text("Full blind mode") }
    public static var blindModeSubtitle: String { L10n.text("Also covers the pitch % and the deck header BPM") }
    public static var blindModeMenuTitle: String { L10n.text("Full Blind Mode") }
    public static var counterMenuTitle: String { L10n.text("Counter") }
    public static var boothMenuTitle: String { L10n.text("Booth") }
    public static var phaseMeterStyleMenuTitle: String { L10n.text("Phase Meter Style") }
    public static var mixTrainerTitle: String { L10n.text("Mix trainer") }
    public static var mixTrainerSubtitle: String { L10n.text("Records your mixes even with the overlays off") }
    public static var mixTrainerMenuTitle: String { L10n.text("Mix Trainer") }
    public static var noOtherMaster: String { L10n.text("No other master") }
}
