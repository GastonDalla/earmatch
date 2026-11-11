import Foundation
import Testing
@testable import EarmatchCore

struct PracticeHistoryTests {
    private static let utc: Calendar = {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC")!
        return calendar
    }()

    private static let day = 86_400.0
    private static let firstDay = Date(timeIntervalSince1970: 1_793_836_800)

    static func mix(endingAt end: Date, error: Double? = 10, match: Double? = 5) -> PracticeMix {
        PracticeMix(startedAt: end.addingTimeInterval(-60), endedAt: end, outgoingDeck: 1, incomingDeck: 2,
                    bothPlayingSeconds: 60, secondsToMatch: match, tempoDriftBPM: match == nil ? nil : 0.01,
                    meanPhaseErrorMilliseconds: error, worstPhaseErrorMilliseconds: error.map { $0 * 2 })
    }

    static func result(endingAt end: Date, error: Double? = 10) -> PracticeResult {
        let mix = mix(endingAt: end, error: error)
        return PracticeResult(mix: mix, trace: PracticeTrace(mixID: mix.id, durationSeconds: 60, matchedAtSeconds: 5,
                                                             samples: [PracticeTraceSample(seconds: 0, errorMilliseconds: 4)]))
    }

    @Test func `recording a mix keeps it and its trace`() {
        var history = PracticeHistory()
        let result = Self.result(endingAt: Self.firstDay)

        history.record(result)

        #expect(history.mixes == [result.mix])
        #expect(history.lastTrace == result.trace)
    }

    @Test func `only the newest two hundred mixes are kept`() {
        var history = PracticeHistory()
        let results = (0 ..< 205).map { Self.result(endingAt: Self.firstDay.addingTimeInterval(Double($0) * 120)) }

        for result in results { history.record(result) }

        #expect(history.mixes.count == PracticeHistory.capacity)
        #expect(history.mixes.first == results[5].mix)
        #expect(history.mixes.last == results[204].mix)
        #expect(history.lastTrace == results[204].trace)
    }

    @Test func `mixes stay in the order they ended`() {
        var history = PracticeHistory()
        let late = Self.result(endingAt: Self.firstDay.addingTimeInterval(600))
        let early = Self.result(endingAt: Self.firstDay)

        history.record(late)
        history.record(early)

        #expect(history.mixes == [early.mix, late.mix])
        #expect(history.lastTrace == early.trace)
    }

    @Test func `progress is the median phase error of each day, oldest first`() throws {
        let mixes = [
            Self.mix(endingAt: Self.firstDay.addingTimeInterval(3_600), error: 30),
            Self.mix(endingAt: Self.firstDay.addingTimeInterval(7_200), error: 10),
            Self.mix(endingAt: Self.firstDay.addingTimeInterval(9_000), error: 14),
            Self.mix(endingAt: Self.firstDay.addingTimeInterval(2 * Self.day + 60), error: 8),
            Self.mix(endingAt: Self.firstDay.addingTimeInterval(2 * Self.day + 120), error: nil, match: nil),
            Self.mix(endingAt: Self.firstDay.addingTimeInterval(2 * Self.day + 180), error: 6),
        ]

        let days = PracticeProgress.days(from: mixes.shuffled(), calendar: Self.utc)

        #expect(days.map(\.day) == [Self.firstDay, Self.firstDay.addingTimeInterval(2 * Self.day)])
        #expect(days.map(\.mixCount) == [3, 3])
        #expect(days.map(\.medianPhaseErrorMilliseconds) == [14, 7])
        #expect(days.map(\.medianSecondsToMatch) == [5, 5])
    }

    @Test func `progress keeps the most recent days`() {
        let mixes = (0 ..< 20).map { Self.mix(endingAt: Self.firstDay.addingTimeInterval(Double($0) * Self.day + 60)) }

        let days = PracticeProgress.days(from: mixes, calendar: Self.utc, limit: 14)

        #expect(days.count == 14)
        #expect(days.first?.day == Self.firstDay.addingTimeInterval(6 * Self.day))
        #expect(days.last?.day == Self.firstDay.addingTimeInterval(19 * Self.day))
    }

    @Test func `the overview leads with the newest mix and its trace`() {
        var history = PracticeHistory()
        let first = Self.result(endingAt: Self.firstDay)
        let second = Self.result(endingAt: Self.firstDay.addingTimeInterval(300), error: 20)
        history.record(first)
        history.record(second)

        let overview = PracticeOverview(history: history, calendar: Self.utc)

        #expect(overview.lastMix == second.mix)
        #expect(overview.lastTrace == second.trace)
        #expect(overview.mixCount == 2)
        #expect(overview.days.map(\.medianPhaseErrorMilliseconds) == [15])
        #expect(PracticeOverview.empty.lastMix == nil)
    }
}
