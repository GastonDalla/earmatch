public enum BPMLimit: String, CaseIterable, Codable, Sendable {
    case oneDecimal
    case noDecimals
    case coverFully
}

public enum WaveformMode: String, CaseIterable, Codable, Sendable {
    case normal
    case split
    case hideBoth
}

public enum PhaseMeterType: String, CaseIterable, Codable, Sendable {
    case box
    case line
    case cdj3000PhaseMeter
    case cdj3000Waveform

    public var isCDJ3000: Bool { self == .cdj3000PhaseMeter || self == .cdj3000Waveform }
}

public enum CounterStyle: String, CaseIterable, Codable, Sendable {
    case cueCountdown
    case barPosition
}

public struct OverlaySettings: Equatable, Codable, Sendable {
    public var isEnabled: Bool
    public var isBPMLimitEnabled: Bool
    public var bpmLimit: BPMLimit
    public var waveformMode: WaveformMode
    public var isPhaseMeterEnabled: Bool
    public var phaseMeterType: PhaseMeterType
    public var counterStyle: CounterStyle
    public var isBlindModeEnabled: Bool
    public var boothPreset: BoothPreset?
    public var isMixTrainerEnabled: Bool

    public init(
        isEnabled: Bool = true,
        isBPMLimitEnabled: Bool = true,
        bpmLimit: BPMLimit = .oneDecimal,
        waveformMode: WaveformMode = .split,
        isPhaseMeterEnabled: Bool = true,
        phaseMeterType: PhaseMeterType = .line,
        counterStyle: CounterStyle = .barPosition,
        isBlindModeEnabled: Bool = false,
        boothPreset: BoothPreset? = nil,
        isMixTrainerEnabled: Bool = true
    ) {
        self.isEnabled = isEnabled
        self.isBPMLimitEnabled = isBPMLimitEnabled
        self.bpmLimit = bpmLimit
        self.waveformMode = waveformMode
        self.isPhaseMeterEnabled = isPhaseMeterEnabled
        self.phaseMeterType = phaseMeterType
        self.counterStyle = counterStyle
        self.isBlindModeEnabled = isBlindModeEnabled
        self.boothPreset = boothPreset
        self.isMixTrainerEnabled = isMixTrainerEnabled
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let defaults = OverlaySettings()
        func value<T: Decodable>(_ key: CodingKeys, _ fallback: T) -> T {
            ((try? container.decodeIfPresent(T.self, forKey: key)) ?? nil) ?? fallback
        }
        isEnabled = value(.isEnabled, defaults.isEnabled)
        isBPMLimitEnabled = value(.isBPMLimitEnabled, defaults.isBPMLimitEnabled)
        bpmLimit = value(.bpmLimit, defaults.bpmLimit)
        waveformMode = value(.waveformMode, defaults.waveformMode)
        isPhaseMeterEnabled = value(.isPhaseMeterEnabled, defaults.isPhaseMeterEnabled)
        phaseMeterType = value(.phaseMeterType, defaults.phaseMeterType)
        counterStyle = value(.counterStyle, defaults.counterStyle)
        isBlindModeEnabled = value(.isBlindModeEnabled, defaults.isBlindModeEnabled)
        boothPreset = (try? container.decodeIfPresent(BoothPreset.self, forKey: .boothPreset)) ?? nil
        isMixTrainerEnabled = value(.isMixTrainerEnabled, defaults.isMixTrainerEnabled)
    }
}
