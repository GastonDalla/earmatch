import Testing
import EarmatchCore

@Suite struct PhaseMeterModelTests {
    let decks = [
        PhaseMeterDeckInput(number: 1, phaseInBar: 0.2, counter: BarPosition(bar: 24, beat: 1)),
        PhaseMeterDeckInput(number: 2, phaseInBar: 3.4, counter: BarPosition(bar: 6, beat: 4)),
    ]

    @Test func aDeckThatIsNotMasterShowsTheMasterRowAboveItsOwn() {
        let display = PhaseMeterModel.display(for: 1, decks: decks, master: 2, style: .box)

        #expect(display.style == .box)
        #expect(display.masterPlayer == 2)
        #expect(display.rows.map(\.role) == [.master, .deck])
        #expect(display.rows.map(\.deckNumber) == [2, 1])
        #expect(display.rows.map(\.counter) == ["06.4", "24.1"])
        #expect(display.rows.map(\.beatInBar) == [3, 0])
        #expect(PhaseMeterDisplay.counterUnit == "Bars")
    }

    @Test func boxesLightTheMastersBeatAndFillThisDecksBeatRedWhenItDiffers() {
        let rows = PhaseMeterModel.display(for: 1, decks: decks, master: 2, style: .box).rows

        #expect(rows[0].boxes == [.idle, .idle, .idle, .masterBeat])
        #expect(rows[1].boxes == [.outOfPhase, .idle, .idle, .idle])
        #expect(rows[1].isOnMastersBeat == false)
        #expect(rows[0].isOnMastersBeat == nil)
    }

    @Test func thisDecksBoxIsBlueWhenItIsOnTheMastersBeat() {
        var inPhase = decks
        inPhase[0].phaseInBar = 3.1
        let rows = PhaseMeterModel.display(for: 1, decks: inPhase, master: 2, style: .box).rows

        #expect(rows[1].boxes == [.idle, .idle, .idle, .inPhase])
        #expect(rows[1].isOnMastersBeat == true)
    }

    @Test func theSyncMasterShowsOnlyItsOwnRowWithoutALabel() {
        let display = PhaseMeterModel.display(for: 2, decks: decks, master: 2, style: .box)

        #expect(display.masterPlayer == nil)
        #expect(display.label == nil)
        #expect(display.rows.count == 1)
        #expect(display.rows[0].role == .deck)
        #expect(display.rows[0].boxes == [.idle, .idle, .idle, .inPhase])
        #expect(display.rows[0].counter == "06.4")
    }

    @Test func withoutASyncMasterEveryDeckShowsOnlyItsOwnRow() {
        let display = PhaseMeterModel.display(for: 1, decks: decks, master: nil, style: .line)

        #expect(display.masterPlayer == nil)
        #expect(display.rows.map(\.deckNumber) == [1])
        #expect(display.rows[0].isOnMastersBeat == nil)
    }

    @Test func thisDecksBoxIsNeutralWhileTheMastersGridIsUnread() {
        let decks = [
            PhaseMeterDeckInput(number: 1, phaseInBar: 1.2, counter: nil),
            PhaseMeterDeckInput(number: 2, phaseInBar: nil, counter: nil),
        ]
        let rows = PhaseMeterModel.display(for: 1, decks: decks, master: 2, style: .box).rows

        #expect(rows[1].boxes == [.idle, .idle, .idle, .idle])
        #expect(rows[1].isOnMastersBeat == nil)
        #expect(rows[1].beatInBar == 1)
        #expect(rows[0].boxes == [.idle, .idle, .idle, .idle])
    }

    @Test func aDeckWithoutAReadingShowsAnIdleRow() throws {
        let decks = [PhaseMeterDeckInput(number: 1, phaseInBar: nil, counter: nil)]
        let row = try #require(PhaseMeterModel.display(for: 1, decks: decks, master: nil, style: .box).rows.first)

        #expect(row.beatInBar == nil)
        #expect(row.ticks.isEmpty)
        #expect(row.boxes == [.idle, .idle, .idle, .idle])
        #expect(row.counter == "--.-")
        #expect(!row.isVacant)
    }

    @Test func theBoxMeterIsLabelledMasterPlayerWithTheNumberInABoxAndCountsOnTheRight() throws {
        let display = PhaseMeterModel.display(for: 1, decks: decks, master: 2, style: .box)
        let label = try #require(display.label)

        #expect(label.lines == ["MASTER", "PLAYER"])
        #expect(label.player == 2)
        #expect(display.counterSide == .right)
        #expect(display.rows.allSatisfy { $0.ticks.isEmpty })
    }

    @Test func lineTicksSitAtTheGridsRealOffsetsWithinOneBarOfNow() throws {
        let decks = [PhaseMeterDeckInput(number: 1, phaseInBar: 1.25, counter: nil)]
        let display = PhaseMeterModel.display(for: 1, decks: decks, master: nil, style: .line)
        let row = try #require(display.rows.first)

        #expect(display.span == 4)
        #expect(row.ticks.map(\.offset) == [-3.25, -2.25, -1.25, -0.25, 0.75, 1.75, 2.75, 3.75])
        #expect(row.ticks.map(\.position) == [-0.8125, -0.5625, -0.3125, -0.0625, 0.1875, 0.4375, 0.6875, 0.9375])
        #expect(row.ticks.filter(\.isDownbeat).map(\.offset) == [-1.25, 2.75])
        #expect(row.fraction == 0.25)
        #expect(row.boxes.isEmpty)
    }

    @Test func lineTicksHangFromTheMasterRulerAndStandOnThisDecksRuler() throws {
        let display = PhaseMeterModel.display(for: 1, decks: decks, master: 2, style: .line)

        #expect(display.rows.map(\.tickDirection) == [.down, .up])
        #expect(display.label?.lines == ["MASTER", "PLAYER"])
        #expect(display.label?.player == 2)
        #expect(display.counterSide == .right)
    }

    @Test func theCDJ3000MeterShowsAboutOnePointTwoBarsEachSide() throws {
        let decks = [PhaseMeterDeckInput(number: 1, phaseInBar: 1.25, counter: nil)]
        let display = PhaseMeterModel.display(for: 1, decks: decks, master: nil, style: .cdj3000PhaseMeter)
        let row = try #require(display.rows.last)

        #expect(display.span == 4.8)
        #expect(row.ticks.map(\.offset) == [-4.25, -3.25, -2.25, -1.25, -0.25, 0.75, 1.75, 2.75, 3.75, 4.75])
        #expect(row.ticks.allSatisfy { abs($0.position) <= 1 })
        #expect(row.ticks.filter(\.isDownbeat).map(\.offset) == [-1.25, 2.75])
    }

    @Test func theCDJ3000MeterCountsOnTheLeftWithoutAMasterPlayerLabel() {
        let display = PhaseMeterModel.display(for: 1, decks: decks, master: 2, style: .cdj3000PhaseMeter)

        #expect(display.counterSide == .left)
        #expect(display.label == nil)
        #expect(display.masterPlayer == 2)
        #expect(display.rows.map(\.role) == [.master, .deck])
        #expect(display.rows.map(\.counter) == ["06.4", "24.1"])
        #expect(display.rows.map(\.tickDirection) == [.down, .up])
        #expect(!display.rows[0].isVacant)
        #expect(!display.rows[0].ticks.isEmpty)
    }

    @Test func onTheCDJ3000TheMasterRulerIsEmptyWhenThisDeckIsMaster() {
        for style in [PhaseMeterType.cdj3000PhaseMeter, .cdj3000Waveform] {
            let display = PhaseMeterModel.display(for: 2, decks: decks, master: 2, style: style)

            #expect(display.rows.map(\.role) == [.master, .deck])
            #expect(display.rows[0].isVacant)
            #expect(display.rows[0].ticks.isEmpty)
            #expect(display.rows[0].beatInBar == nil)
            #expect(display.rows[0].counter == "--.-")
            #expect(display.rows[1].counter == "06.4")
            #expect(!display.rows[1].ticks.isEmpty)
            #expect(display.masterPlayer == nil)
            #expect(!display.showsMasterWaveform)
        }
    }

    @Test func onTheCDJ3000TheMasterRulerIsEmptyWithoutASyncMaster() {
        let display = PhaseMeterModel.display(for: 1, decks: decks, master: nil, style: .cdj3000PhaseMeter)

        #expect(display.rows.count == 2)
        #expect(display.rows[0].isVacant)
        #expect(display.rows[0].counter == "--.-")
        #expect(display.rows[1].counter == "24.1")
    }

    @Test func theCDJ3000WaveformShowsTheMastersWaveformAndItsNumberInAnOrangeBox() throws {
        let display = PhaseMeterModel.display(for: 1, decks: decks, master: 2, style: .cdj3000Waveform)
        let label = try #require(display.label)

        #expect(display.showsMasterWaveform)
        #expect(label.lines.isEmpty)
        #expect(label.player == 2)
        #expect(display.counterSide == .left)
        #expect(display.rows.map(\.counter) == ["06.4", "24.1"])
    }

    @Test func otherStylesNeverShowTheMastersWaveform() {
        for style in [PhaseMeterType.box, .line, .cdj3000PhaseMeter] {
            #expect(!PhaseMeterModel.display(for: 1, decks: decks, master: 2, style: style).showsMasterWaveform)
        }
    }
}
