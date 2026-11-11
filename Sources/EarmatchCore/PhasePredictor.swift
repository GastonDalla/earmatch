import Foundation

public struct PhasePredictor: Sendable {
    public struct Anchor: Equatable, Sendable {
        public var time: Double
        public var phaseInBar: Double?
        public var beatsPerSecond: Double?
        public var isMeasured: Bool

        public init(time: Double, phaseInBar: Double?, beatsPerSecond: Double?, isMeasured: Bool) {
            self.time = time
            self.phaseInBar = phaseInBar
            self.beatsPerSecond = beatsPerSecond
            self.isMeasured = isMeasured
        }
    }

    public static let maxExtrapolation = PhaseTracker.holdDuration + 0.05
    static let followTime = 0.025
    static let settleTime = 0.008
    static let jumpTolerance = PhaseTracker.phaseTolerance
    static let maxRefreshGap = 0.1

    private struct Measured {
        var time: Double
        var phase: Double
        var beatsPerSecond: Double?
    }

    private var measured: Measured?
    private var drawn: (time: Double, phase: Double)?

    public init() {}

    public var beatsPerSecond: Double { measured?.beatsPerSecond ?? 0 }

    public mutating func update(_ anchor: Anchor) {
        guard let phase = anchor.phaseInBar else {
            reset()
            return
        }
        if anchor.isMeasured || measured == nil {
            measured = Measured(time: anchor.time, phase: phase, beatsPerSecond: anchor.beatsPerSecond)
        } else {
            measured?.beatsPerSecond = anchor.beatsPerSecond
        }
    }

    public mutating func reset() {
        measured = nil
        drawn = nil
    }

    public func predictedPhase(at time: Double) -> Double? {
        guard let measured, time - measured.time <= Self.maxExtrapolation else { return nil }
        return positiveModulo(measured.phase + (measured.beatsPerSecond ?? 0) * max(0, time - measured.time),
                              BeatGridDetector.beatsPerBar)
    }

    public mutating func phase(at time: Double) -> Double? {
        guard let target = predictedPhase(at: time) else {
            drawn = nil
            return nil
        }
        let rate = measured?.beatsPerSecond ?? 0
        guard let last = drawn, time > last.time, time - last.time <= Self.maxRefreshGap else {
            drawn = (time, target)
            return target
        }
        let elapsed = time - last.time
        let coasting = last.phase + rate * elapsed
        let error = PhaseTracker.circularBeats(target - coasting)
        var step: Double
        if abs(error) > Self.jumpTolerance {
            step = PhaseTracker.circularBeats(target - last.phase)
        } else {
            let gain = 1 - exp(-elapsed / (rate == 0 ? Self.settleTime : Self.followTime))
            step = rate * elapsed + gain * error
            if rate > 0 { step = max(step, 0) } else if rate < 0 { step = min(step, 0) }
        }
        let phase = positiveModulo(last.phase + step, BeatGridDetector.beatsPerBar)
        drawn = (time, phase)
        return phase
    }
}
