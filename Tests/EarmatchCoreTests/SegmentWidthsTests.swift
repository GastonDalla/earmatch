import Testing
import EarmatchCore

@Suite struct SegmentWidthsTests {
    @Test func labelsThatFitShareTheRowEqually() {
        #expect(SegmentWidths.distribute(ideal: [40, 52, 61], available: 300) == [100, 100, 100])
    }

    @Test func aLongLabelKeepsItsWidthAndTheOthersShareTheRest() {
        let widths = SegmentWidths.distribute(ideal: [40, 40, 90, 70], available: 290)
        #expect(widths == [65, 65, 90, 70])
        #expect(widths.reduce(0, +) == 290)
    }

    @Test func whenNothingFitsEverySegmentShrinksInProportion() {
        let widths = SegmentWidths.distribute(ideal: [100, 50, 50], available: 100)
        #expect(widths == [50, 25, 25])
    }

    @Test func noSegmentsOrNoRoomGiveNoWidth() {
        #expect(SegmentWidths.distribute(ideal: [], available: 200).isEmpty)
        #expect(SegmentWidths.distribute(ideal: [30, 40], available: 0) == [0, 0])
        #expect(SegmentWidths.distribute(ideal: [30, 40], available: -10) == [0, 0])
    }
}
