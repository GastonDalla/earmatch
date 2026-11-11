import Foundation

public struct PracticeHistory: Equatable, Sendable {
    public static let capacity = 200

    public private(set) var mixes: [PracticeMix]
    public private(set) var lastTrace: PracticeTrace?

    public init(mixes: [PracticeMix] = [], lastTrace: PracticeTrace? = nil) {
        self.mixes = Array(mixes.sorted { $0.endedAt < $1.endedAt }.suffix(Self.capacity))
        self.lastTrace = lastTrace
    }

    public mutating func record(_ result: PracticeResult) {
        let index = mixes.lastIndex { $0.endedAt <= result.mix.endedAt }.map { $0 + 1 } ?? 0
        mixes.insert(result.mix, at: index)
        if mixes.count > Self.capacity { mixes.removeFirst(mixes.count - Self.capacity) }
        lastTrace = result.trace
    }
}

public struct PracticeDay: Equatable, Hashable, Sendable, Identifiable {
    public var day: Date
    public var mixCount: Int
    public var medianPhaseErrorMilliseconds: Double?
    public var medianSecondsToMatch: Double?

    public var id: Date { day }

    public init(day: Date, mixCount: Int, medianPhaseErrorMilliseconds: Double?, medianSecondsToMatch: Double?) {
        self.day = day
        self.mixCount = mixCount
        self.medianPhaseErrorMilliseconds = medianPhaseErrorMilliseconds
        self.medianSecondsToMatch = medianSecondsToMatch
    }
}

public enum PracticeProgress {
    public static let dayLimit = 14

    public static func days(from mixes: [PracticeMix], calendar: Calendar, limit: Int = dayLimit) -> [PracticeDay] {
        let byDay = Dictionary(grouping: mixes) { calendar.startOfDay(for: $0.endedAt) }
        return byDay.keys.sorted().suffix(limit).map { day in
            let mixes = byDay[day] ?? []
            return PracticeDay(day: day, mixCount: mixes.count,
                               medianPhaseErrorMilliseconds: median(mixes.compactMap(\.meanPhaseErrorMilliseconds)),
                               medianSecondsToMatch: median(mixes.compactMap(\.secondsToMatch)))
        }
    }

    static func median(_ values: [Double]) -> Double? {
        guard !values.isEmpty else { return nil }
        let sorted = values.sorted()
        let middle = sorted.count / 2
        return sorted.count.isMultiple(of: 2) ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle]
    }
}

public struct PracticeOverview: Equatable, Sendable {
    public var lastMix: PracticeMix?
    public var lastTrace: PracticeTrace?
    public var mixCount: Int
    public var days: [PracticeDay]

    public static let empty = PracticeOverview(lastMix: nil, lastTrace: nil, mixCount: 0, days: [])

    public init(lastMix: PracticeMix?, lastTrace: PracticeTrace?, mixCount: Int, days: [PracticeDay]) {
        self.lastMix = lastMix
        self.lastTrace = lastTrace
        self.mixCount = mixCount
        self.days = days
    }

    public init(history: PracticeHistory, calendar: Calendar) {
        self.init(lastMix: history.mixes.last,
                  lastTrace: history.lastTrace,
                  mixCount: history.mixes.count,
                  days: PracticeProgress.days(from: history.mixes, calendar: calendar))
    }
}
