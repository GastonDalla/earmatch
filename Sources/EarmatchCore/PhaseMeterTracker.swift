import Foundation

public struct PhaseTracker: Sendable {
    public enum Status: Equatable, Sendable {
        case live
        case held
        case lost
    }

    public struct Estimate: Equatable, Sendable {
        public var phaseInBar: Double?
        public var beatSpacing: Double?
        public var status: Status

        public init(phaseInBar: Double?, beatSpacing: Double?, status: Status) {
            self.phaseInBar = phaseInBar
            self.beatSpacing = beatSpacing
            self.status = status
        }
    }

    public static let holdDuration = 0.25
    public static let spacingTolerance = 0.15
    static let phaseTolerance = 0.25
    static let framesToConfirm = 3

    private struct Sample {
        let time: Double
        let phase: Double
        let spacing: Double
    }

    private var good: Sample?
    private var candidate: (sample: Sample, count: Int)?
    private var rate = BeatRateEstimator()
    public var beatsPerSecond: Double? { rate.beatsPerSecond }

    public init() {}

    public mutating func update(_ reading: BeatGridReading?, at time: Double) -> Estimate {
        guard let reading, let phase = reading.phaseInBar else { return hold(at: time) }
        let sample = Sample(time: time, phase: phase, spacing: reading.beatSpacing)
        guard let good, time - good.time <= Self.holdDuration else {
            candidate = nil
            rate.reset()
            return accept(sample)
        }
        if agrees(sample, with: good) {
            candidate = nil
            return accept(sample)
        }
        if let pending = candidate, agrees(sample, with: pending.sample) {
            if pending.count + 1 >= Self.framesToConfirm {
                candidate = nil
                rate.restart()
                return accept(sample)
            }
            candidate = (sample, pending.count + 1)
        } else {
            candidate = (sample, 1)
        }
        return hold(at: time)
    }

    private mutating func accept(_ sample: Sample) -> Estimate {
        rate.add(time: sample.time, phase: sample.phase, quantum: 1 / max(sample.spacing, 1))
        good = sample
        return Estimate(phaseInBar: sample.phase, beatSpacing: sample.spacing, status: .live)
    }

    private func hold(at time: Double) -> Estimate {
        guard let good, time - good.time <= Self.holdDuration else {
            return Estimate(phaseInBar: nil, beatSpacing: nil, status: .lost)
        }
        return Estimate(phaseInBar: predictedPhase(from: good, at: time), beatSpacing: good.spacing, status: .held)
    }

    private func agrees(_ sample: Sample, with reference: Sample) -> Bool {
        abs(sample.spacing / reference.spacing - 1) <= Self.spacingTolerance
            && abs(Self.circularBeats(sample.phase - predictedPhase(from: reference, at: sample.time))) <= Self.phaseTolerance
    }

    private func predictedPhase(from sample: Sample, at time: Double) -> Double {
        positiveModulo(sample.phase + (beatsPerSecond ?? 0) * (time - sample.time), BeatGridDetector.beatsPerBar)
    }

    static func circularBeats(_ difference: Double) -> Double {
        positiveModulo(difference + 2, BeatGridDetector.beatsPerBar) - 2
    }
}

struct BeatRateEstimator: Sendable {
    static let window = 0.15
    static let minimumPairSpan = 0.02
    static let stillSpanRange = 0.015 ... 0.1

    private(set) var beatsPerSecond: Double?
    private var samples: [(time: Double, beats: Double)] = []
    private var slopes: [Double] = []

    init() {
        samples.reserveCapacity(32)
        slopes.reserveCapacity(512)
    }

    mutating func reset() {
        samples.removeAll(keepingCapacity: true)
        beatsPerSecond = nil
    }

    mutating func restart() {
        samples.removeAll(keepingCapacity: true)
    }

    mutating func add(time: Double, phase: Double, quantum: Double) {
        let previous = samples.last
        if let previous, time <= previous.time { return }
        let beats = previous.map { $0.beats + PhaseTracker.circularBeats(phase - $0.beats) } ?? phase
        let tolerance = quantum / 4
        samples.append((time, beats))
        if let firstKept = samples.firstIndex(where: { time - $0.time <= Self.window }), firstKept > 0 {
            samples.removeFirst(firstKept)
        }

        if beatsPerSecond == 0, let previous {
            guard abs(beats - previous.beats) > tolerance else { return }
            samples.removeAll(keepingCapacity: true)
            samples.append(previous)
            samples.append((time, beats))
            beatsPerSecond = (beats - previous.beats) / (time - previous.time)
            return
        }
        if let stillSince = stillSince(tolerance: tolerance, quantum: quantum) {
            beatsPerSecond = 0
            samples.removeFirst(stillSince)
            return
        }
        beatsPerSecond = medianSlope() ?? beatsPerSecond
    }

    private func stillSince(tolerance: Double, quantum: Double) -> Int? {
        guard let newest = samples.last else { return nil }
        var first = samples.count - 1
        while first > 0, abs(samples[first - 1].beats - newest.beats) <= tolerance { first -= 1 }
        let speed = max(abs(beatsPerSecond ?? 0), 0.5)
        let needed = min(max(quantum / speed, Self.stillSpanRange.lowerBound), Self.stillSpanRange.upperBound)
        guard samples.count - first >= 3, newest.time - samples[first].time >= needed else { return nil }
        return first
    }

    private mutating func medianSlope() -> Double? {
        guard samples.count >= 2 else { return nil }
        slopes.removeAll(keepingCapacity: true)
        collectSlopes(minimumSpan: Self.minimumPairSpan)
        if slopes.isEmpty, beatsPerSecond == nil { collectSlopes(minimumSpan: 0) }
        guard !slopes.isEmpty else { return nil }
        slopes.sort()
        let middle = slopes.count / 2
        return slopes.count % 2 == 1 ? slopes[middle] : (slopes[middle - 1] + slopes[middle]) / 2
    }

    private mutating func collectSlopes(minimumSpan: Double) {
        for i in samples.indices {
            for j in (i + 1) ..< samples.count {
                let span = samples[j].time - samples[i].time
                if span > 0, span >= minimumSpan { slopes.append((samples[j].beats - samples[i].beats) / span) }
            }
        }
    }
}
