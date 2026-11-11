import Foundation

public struct PracticeMix: Codable, Equatable, Hashable, Sendable, Identifiable {
    public var id: UUID
    public var startedAt: Date
    public var endedAt: Date
    public var outgoingDeck: Int
    public var incomingDeck: Int
    public var bothPlayingSeconds: Double
    public var secondsToMatch: Double?
    public var tempoDriftBPM: Double?
    public var meanPhaseErrorMilliseconds: Double?
    public var worstPhaseErrorMilliseconds: Double?

    public init(id: UUID = UUID(), startedAt: Date, endedAt: Date, outgoingDeck: Int, incomingDeck: Int,
                bothPlayingSeconds: Double, secondsToMatch: Double?, tempoDriftBPM: Double?,
                meanPhaseErrorMilliseconds: Double?, worstPhaseErrorMilliseconds: Double?) {
        self.id = id
        self.startedAt = startedAt
        self.endedAt = endedAt
        self.outgoingDeck = outgoingDeck
        self.incomingDeck = incomingDeck
        self.bothPlayingSeconds = bothPlayingSeconds
        self.secondsToMatch = secondsToMatch
        self.tempoDriftBPM = tempoDriftBPM
        self.meanPhaseErrorMilliseconds = meanPhaseErrorMilliseconds
        self.worstPhaseErrorMilliseconds = worstPhaseErrorMilliseconds
    }
}

public struct PracticeTraceSample: Codable, Equatable, Hashable, Sendable {
    public var seconds: Double
    public var errorMilliseconds: Double

    public init(seconds: Double, errorMilliseconds: Double) {
        self.seconds = seconds
        self.errorMilliseconds = errorMilliseconds
    }

    private enum CodingKeys: String, CodingKey {
        case seconds = "t"
        case errorMilliseconds = "e"
    }
}

public struct PracticeTrace: Codable, Equatable, Hashable, Sendable {
    public var mixID: UUID
    public var durationSeconds: Double
    public var matchedAtSeconds: Double?
    public var samples: [PracticeTraceSample]

    public init(mixID: UUID, durationSeconds: Double, matchedAtSeconds: Double?, samples: [PracticeTraceSample]) {
        self.mixID = mixID
        self.durationSeconds = durationSeconds
        self.matchedAtSeconds = matchedAtSeconds
        self.samples = samples
    }

    public func errorMilliseconds(at seconds: Double) -> Double? {
        guard let first = samples.first, let last = samples.last else { return nil }
        if seconds <= first.seconds { return first.errorMilliseconds }
        if seconds >= last.seconds { return last.errorMilliseconds }
        var low = 0
        var high = samples.count - 1
        while high - low > 1 {
            let middle = (low + high) / 2
            if samples[middle].seconds <= seconds { low = middle } else { high = middle }
        }
        let before = samples[low]
        let after = samples[high]
        let span = after.seconds - before.seconds
        guard span > 0 else { return before.errorMilliseconds }
        return before.errorMilliseconds + (after.errorMilliseconds - before.errorMilliseconds) * (seconds - before.seconds) / span
    }

    public var worstSample: PracticeTraceSample? {
        let matched = samples.filter { $0.seconds >= (matchedAtSeconds ?? .infinity) }
        return matched.max { abs($0.errorMilliseconds) < abs($1.errorMilliseconds) }
    }
}

public struct PracticeResult: Equatable, Sendable {
    public var mix: PracticeMix
    public var trace: PracticeTrace

    public init(mix: PracticeMix, trace: PracticeTrace) {
        self.mix = mix
        self.trace = trace
    }
}

public struct PracticeLiveStatus: Equatable, Sendable {
    public var outgoingDeck: Int
    public var incomingDeck: Int
    public var elapsedSeconds: Double
    public var bothPlayingSeconds: Double
    public var tempoDifferenceBPM: Double?
    public var secondsToMatch: Double?
    public var phaseErrorMilliseconds: Double?
    public var meanPhaseErrorMilliseconds: Double?

    public init(outgoingDeck: Int, incomingDeck: Int, elapsedSeconds: Double, bothPlayingSeconds: Double,
                tempoDifferenceBPM: Double?, secondsToMatch: Double?, phaseErrorMilliseconds: Double?,
                meanPhaseErrorMilliseconds: Double?) {
        self.outgoingDeck = outgoingDeck
        self.incomingDeck = incomingDeck
        self.elapsedSeconds = elapsedSeconds
        self.bothPlayingSeconds = bothPlayingSeconds
        self.tempoDifferenceBPM = tempoDifferenceBPM
        self.secondsToMatch = secondsToMatch
        self.phaseErrorMilliseconds = phaseErrorMilliseconds
        self.meanPhaseErrorMilliseconds = meanPhaseErrorMilliseconds
    }

    public var isMatched: Bool { secondsToMatch != nil }
    public var isLongEnough: Bool { bothPlayingSeconds >= PracticeSession.minimumMixSeconds }
}
