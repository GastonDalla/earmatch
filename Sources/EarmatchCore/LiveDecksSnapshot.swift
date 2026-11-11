public struct LiveDeckSnapshot: Equatable, Sendable {
    public var number: Int
    public var title: String?
    public var isMaster: Bool
    public var meter: PhaseMeterDisplay?
    public var phaseInBar: Double?
    public var beatsPerSecond: Double

    public var isPlaying: Bool { beatsPerSecond > 0.05 }

    public init(number: Int, title: String?, isMaster: Bool, meter: PhaseMeterDisplay?,
                phaseInBar: Double? = nil, beatsPerSecond: Double = 0) {
        self.number = number
        self.title = title
        self.isMaster = isMaster
        self.meter = meter
        self.phaseInBar = phaseInBar
        self.beatsPerSecond = beatsPerSecond
    }
}

public struct LiveDecksSnapshot: Equatable, Sendable {
    public var decks: [LiveDeckSnapshot]
    public var framesPerSecond: Double
    public var latencyMilliseconds: Double

    public init(decks: [LiveDeckSnapshot], framesPerSecond: Double, latencyMilliseconds: Double) {
        self.decks = decks
        self.framesPerSecond = framesPerSecond
        self.latencyMilliseconds = latencyMilliseconds
    }

    public static let empty = LiveDecksSnapshot(decks: [], framesPerSecond: 0, latencyMilliseconds: 0)
}
