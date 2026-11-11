import Testing
import EarmatchCore

@Suite struct PhaseMeterTrackerTests {
    let frame = 1.0 / 120

    func reading(phase: Double, spacing: Double = 30) -> BeatGridReading {
        BeatGridReading(beatSpacing: spacing, beatXs: [], downbeatXs: [], phaseInBar: phase)
    }

    func play(_ tracker: inout PhaseTracker, rate: Double = 2, spacing: Double = 30) -> Double {
        var time = 0.0
        for index in 0 ..< 60 {
            time = Double(index) * frame
            _ = tracker.update(reading(phase: (1 + rate * time).truncatingRemainder(dividingBy: 4), spacing: spacing), at: time)
        }
        return time
    }

    @Test func aGoodReadingIsShownAsIs() {
        var tracker = PhaseTracker()
        let estimate = tracker.update(reading(phase: 1.5), at: 0)

        #expect(estimate == PhaseTracker.Estimate(phaseInBar: 1.5, beatSpacing: 30, status: .live))
    }

    @Test func aMissedFrameIsHeldAndAdvancedAtTheMeasuredTempo() throws {
        var tracker = PhaseTracker()
        let last = play(&tracker)
        let lastPhase = 1 + 2 * last

        let estimate = tracker.update(nil, at: last + 0.1)

        #expect(estimate.status == .held)
        #expect(abs(try #require(estimate.phaseInBar) - (lastPhase + 0.2)) < 0.02)
    }

    @Test func afterAQuarterSecondWithoutAGoodReadingTheMeterShowsNoGrid() {
        var tracker = PhaseTracker()
        let last = play(&tracker)

        let estimate = tracker.update(nil, at: last + 0.3)

        #expect(estimate.status == .lost)
        #expect(estimate.phaseInBar == nil)
    }

    @Test func oneFrameWithAWildSpacingDoesNotMoveTheMeter() throws {
        var tracker = PhaseTracker()
        let last = play(&tracker)

        let estimate = tracker.update(reading(phase: 3.6, spacing: 45), at: last + frame)

        #expect(estimate.status == .held)
        #expect(estimate.beatSpacing == 30)
        #expect(abs(try #require(estimate.phaseInBar) - (1 + 2 * (last + frame))) < 0.02)
    }

    @Test func aSpacingChangeThatPersistsIsAZoomChange() {
        var tracker = PhaseTracker()
        var time = play(&tracker)
        var estimate = PhaseTracker.Estimate(phaseInBar: nil, beatSpacing: nil, status: .lost)
        for _ in 0 ..< 3 {
            time += frame
            estimate = tracker.update(reading(phase: (1 + 2 * time).truncatingRemainder(dividingBy: 4), spacing: 45), at: time)
        }

        #expect(estimate.status == .live)
        #expect(estimate.beatSpacing == 45)
    }

    @Test func aPhaseJumpIsHeldForTwoFramesAndAcceptedOnTheThird() throws {
        var tracker = PhaseTracker()
        var time = play(&tracker)
        func jumped(_ time: Double) -> Double { (3 + 2 * time).truncatingRemainder(dividingBy: 4) }
        func old(_ time: Double) -> Double { (1 + 2 * time).truncatingRemainder(dividingBy: 4) }

        for _ in 0 ..< 2 {
            time += frame
            let held = tracker.update(reading(phase: jumped(time)), at: time)
            #expect(held.status == .held)
            #expect(abs(try #require(held.phaseInBar) - old(time)) < 0.02)
        }

        time += frame
        let accepted = tracker.update(reading(phase: jumped(time)), at: time)
        #expect(accepted.status == .live)
        #expect(abs(try #require(accepted.phaseInBar) - jumped(time)) < 1e-9)
    }

    @Test func aPausedDeckIsHeldInPlace() throws {
        var tracker = PhaseTracker()
        let last = play(&tracker, rate: 0)

        let estimate = tracker.update(nil, at: last + 0.2)

        #expect(estimate.status == .held)
        #expect(abs(try #require(estimate.phaseInBar) - 1) < 0.01)
    }

    @Test func aFreshReadingAfterTheGridWasLostIsShownAtOnce() {
        var tracker = PhaseTracker()
        let last = play(&tracker)
        _ = tracker.update(nil, at: last + 0.5)

        let estimate = tracker.update(reading(phase: 0.5, spacing: 60), at: last + 0.6)

        #expect(estimate == PhaseTracker.Estimate(phaseInBar: 0.5, beatSpacing: 60, status: .live))
    }

    @discardableResult
    func feed(_ tracker: inout PhaseTracker, from start: Double, to end: Double, phase: (Double) -> Double) -> Double {
        let step = 1.0 / 110
        var time = start
        var last = start
        while time <= end + 1e-9 {
            _ = tracker.update(reading(phase: quantized(phase(time))), at: time)
            last = time
            time += step
        }
        return last
    }

    @Test func theTempoIsMeasuredDespiteThePixelRounding() throws {
        var tracker = PhaseTracker()
        feed(&tracker, from: 0, to: 0.5) { 1 + 2.25 * $0 }

        #expect(abs(try #require(tracker.beatsPerSecond) - 2.25) < 0.05)
    }

    @Test func oneSlightlyWrongReadingBarelyMovesTheTempo() throws {
        var tracker = PhaseTracker()
        let last = feed(&tracker, from: 0, to: 0.5) { 1 + 2.25 * $0 }
        let wrong = last + 1.0 / 110
        _ = tracker.update(reading(phase: quantized(1 + 2.25 * wrong + 0.15)), at: wrong)
        let next = wrong + 1.0 / 110
        _ = tracker.update(reading(phase: quantized(1 + 2.25 * next)), at: next)

        #expect(abs(try #require(tracker.beatsPerSecond) - 2.25) < 0.05)
    }

    @Test func aPausedDeckMeasuresZeroWithinFourFrames() throws {
        var tracker = PhaseTracker()
        let last = feed(&tracker, from: 0, to: 0.5) { 1 + 2.25 * $0 }
        feed(&tracker, from: last + 1.0 / 110, to: last + 4.0 / 110) { _ in 1 + 2.25 * 0.5 }

        #expect(tracker.beatsPerSecond == 0)
    }

    @Test func aResumedDeckMeasuresItsTempoAgainQuicklyWithoutOvershooting() throws {
        var tracker = PhaseTracker()
        let step = 1.0 / 110
        let last = feed(&tracker, from: 0, to: 0.3) { _ in 2 }
        #expect(tracker.beatsPerSecond == 0)

        var time = last
        var rates: [Double] = []
        while time < last + 0.1 {
            time += step
            _ = tracker.update(reading(phase: quantized(2 + 2.25 * (time - last))), at: time)
            rates.append(tracker.beatsPerSecond ?? 0)
        }
        #expect(rates.allSatisfy { $0 <= 2.25 * 1.15 })
        #expect(abs(try #require(rates.last) - 2.25) < 0.1)
        #expect(abs(rates[5] - 2.25) < 2.25 * 0.3)
    }

    @Test func aSeekKeepsTheTempo() throws {
        var tracker = PhaseTracker()
        var time = play(&tracker)
        for _ in 0 ..< 3 {
            time += frame
            _ = tracker.update(reading(phase: (3 + 2 * time).truncatingRemainder(dividingBy: 4)), at: time)
        }

        #expect(abs(try #require(tracker.beatsPerSecond) - 2) < 0.05)
    }

    @Test func afterTheGridWasLostTheTempoIsUnknownUntilMeasuredAgain() {
        var tracker = PhaseTracker()
        let last = play(&tracker)
        _ = tracker.update(nil, at: last + 0.5)

        _ = tracker.update(reading(phase: 0.5), at: last + 0.6)

        #expect(tracker.beatsPerSecond == nil)
    }
}
