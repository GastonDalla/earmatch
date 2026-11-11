public struct RetrySchedule: Equatable, Sendable {
    public enum Reason: Equatable, Sendable {
        case failure
        case permissionDenied
        case windowOffScreen
    }

    public struct Timer: Equatable, Sendable {
        public let delay: Double
        public let fireTime: Double

        public init(delay: Double, fireTime: Double) {
            self.delay = delay
            self.fireTime = fireTime
        }
    }

    public static let failureDelay = 1.0
    public static let permissionDelay = 2.0
    public static let offScreenDelays: ClosedRange<Double> = 0.1 ... 2

    private var armedFireTime: Double?
    private var nextAttempt = -Double.infinity
    private var offScreenLooks = 0

    public init() {}

    public mutating func retry(_ reason: Reason, at now: Double) -> Timer? {
        let delay: Double
        switch reason {
        case .failure: delay = Self.failureDelay
        case .permissionDenied: delay = Self.permissionDelay
        case .windowOffScreen:
            delay = min(Self.offScreenDelays.lowerBound * Double(1 << min(offScreenLooks, 16)), Self.offScreenDelays.upperBound)
            offScreenLooks += 1
        }
        let fireTime = now + delay
        if let armedFireTime, armedFireTime <= fireTime {
            nextAttempt = armedFireTime
            return nil
        }
        nextAttempt = fireTime
        armedFireTime = fireTime
        return Timer(delay: delay, fireTime: fireTime)
    }

    public func allowsAttempt(at now: Double) -> Bool {
        now >= nextAttempt
    }

    public mutating func attemptStarted() {
        armedFireTime = nil
    }

    public mutating func windowFound() {
        offScreenLooks = 0
    }

    public mutating func reset() {
        offScreenLooks = 0
    }

    public mutating func fire(_ timer: Timer) -> Bool {
        guard armedFireTime == timer.fireTime else { return false }
        armedFireTime = nil
        return true
    }
}
