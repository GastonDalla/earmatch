import Testing
import EarmatchCore

@Suite struct BarCounterTests {
    @Test func matchesTheBarTextRekordboxDrawsNearThePlayhead() {
        #expect(BarCounter.position(elapsedSeconds: 10.8, bpm: 124, phaseInBar: 2.3).label == "06.3")
        #expect(BarCounter.position(elapsedSeconds: 42.0, bpm: 135, phaseInBar: 2.5).label == "24.3")
    }

    @Test func theNextBarStartsWhenThePixelDownbeatPassesEvenIfTheClockLags() {
        #expect(BarCounter.position(elapsedSeconds: 11.5, bpm: 124, phaseInBar: 0.1).label == "07.1")
    }

    @Test func theBarHoldsUntilThePixelDownbeatEvenIfTheClockRunsAhead() {
        #expect(BarCounter.position(elapsedSeconds: 11.7, bpm: 124, phaseInBar: 3.9).label == "06.4")
    }

    @Test func withoutAPixelReadingTheClockDecides() {
        #expect(BarCounter.position(elapsedSeconds: 10.8, bpm: 124, phaseInBar: nil).label == "06.3")
    }

    @Test func theFirstBeatIsBarOneBeatOne() {
        let start = BarCounter.position(elapsedSeconds: 0, bpm: 124, phaseInBar: 0)
        #expect(start == BarPosition(bar: 1, beat: 1))
        #expect(start.label == "01.1")
    }
}
