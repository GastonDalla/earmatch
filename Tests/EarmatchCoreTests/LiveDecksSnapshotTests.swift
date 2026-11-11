import Testing
import EarmatchCore

@Suite struct LiveDecksSnapshotTests {
    @Test func aPausedDeckIsNotPlaying() {
        let deck = LiveDeckSnapshot(number: 1, title: nil, isMaster: false, meter: nil, phaseInBar: 1.5, beatsPerSecond: 0)
        #expect(!deck.isPlaying)
    }

    @Test func aDeckAtTempoIsPlaying() {
        let deck = LiveDeckSnapshot(number: 1, title: nil, isMaster: true, meter: nil, phaseInBar: 3.9, beatsPerSecond: 2.25)
        #expect(deck.isPlaying)
        #expect(deck.beatsPerSecond * 60 == 135)
    }

    @Test func newFieldsDefaultToUnknownAndPaused() {
        let deck = LiveDeckSnapshot(number: 2, title: "Track", isMaster: false, meter: nil)
        #expect(deck.phaseInBar == nil)
        #expect(deck.beatsPerSecond == 0)
        #expect(!deck.isPlaying)
    }
}
