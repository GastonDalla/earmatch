import Foundation

public struct StallBackoff: Sendable {
    public let duration: TimeInterval
    private var resumeAt: Date?

    public init(duration: TimeInterval) {
        self.duration = duration
    }

    public func allowsPoll(at now: Date) -> Bool {
        guard let resumeAt else { return true }
        return now >= resumeAt
    }

    public mutating func recordStall(at now: Date) {
        resumeAt = now.addingTimeInterval(duration)
    }
}
