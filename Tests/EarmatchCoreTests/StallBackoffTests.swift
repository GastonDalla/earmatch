import Foundation
import Testing
import EarmatchCore

@Suite struct StallBackoffTests {
    let start = Date(timeIntervalSinceReferenceDate: 1000)

    @Test func pollsFreelyUntilAStall() {
        #expect(StallBackoff(duration: 2).allowsPoll(at: start))
    }

    @Test func waitsTheFullBackoffAfterAStall() {
        var backoff = StallBackoff(duration: 2)
        backoff.recordStall(at: start)
        #expect(!backoff.allowsPoll(at: start.addingTimeInterval(1.9)))
        #expect(backoff.allowsPoll(at: start.addingTimeInterval(2.0)))
    }

    @Test func aNewStallRestartsTheWait() {
        var backoff = StallBackoff(duration: 2)
        backoff.recordStall(at: start)
        backoff.recordStall(at: start.addingTimeInterval(2.5))
        #expect(!backoff.allowsPoll(at: start.addingTimeInterval(4)))
        #expect(backoff.allowsPoll(at: start.addingTimeInterval(4.5)))
    }
}
