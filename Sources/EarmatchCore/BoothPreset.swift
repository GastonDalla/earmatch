public enum BoothPreset: String, CaseIterable, Codable, Sendable {
    case cdj3000
    case cdj2000NXS2
    case xdj1000

    public var phaseMeterType: PhaseMeterType {
        switch self {
        case .cdj3000: .cdj3000Waveform
        case .cdj2000NXS2, .xdj1000: .box
        }
    }

    public var counterStyle: CounterStyle { .cueCountdown }
    public var bpmLimit: BPMLimit { .oneDecimal }
    public var waveformMode: WaveformMode { .split }

    public func apply(to settings: inout OverlaySettings) {
        settings.isPhaseMeterEnabled = true
        settings.phaseMeterType = phaseMeterType
        settings.counterStyle = counterStyle
        settings.isBPMLimitEnabled = true
        settings.bpmLimit = bpmLimit
        settings.waveformMode = waveformMode
        settings.boothPreset = self
    }

    public func matches(_ settings: OverlaySettings) -> Bool {
        settings.isPhaseMeterEnabled && settings.phaseMeterType == phaseMeterType && settings.counterStyle == counterStyle
            && settings.isBPMLimitEnabled && settings.bpmLimit == bpmLimit && settings.waveformMode == waveformMode
    }

    public static func selected(in settings: OverlaySettings) -> BoothPreset? {
        if let chosen = settings.boothPreset, chosen.matches(settings) { return chosen }
        let matching = allCases.filter { $0.matches(settings) }
        return matching.count == 1 ? matching[0] : nil
    }
}
