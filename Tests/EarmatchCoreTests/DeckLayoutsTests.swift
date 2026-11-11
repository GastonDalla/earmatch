import CoreGraphics
import Testing
import EarmatchCore

@Suite struct DeckLayoutsTests {
    static func layout(_ name: String) throws -> RekordboxLayout {
        RekordboxLayout.locate(in: try Fixture.snapshot(name))
    }

    @Test func eachLayoutNameMapsToItsDecksAndStrips() {
        #expect(DeckArrangement.allCases.map(\.rawValue) == ["2Deck Horizontal", "2Deck Vertical", "4Deck Horizontal", "4Deck Vertical"])
        #expect(DeckArrangement.allCases.map(\.deckCount) == [2, 2, 4, 4])
        #expect(DeckArrangement.allCases.map(\.orientation) == [.horizontal, .vertical, .horizontal, .vertical])
        #expect(DeckArrangement.fourDeckVertical.stripDecks == [3, 1, 2, 4])
        #expect(DeckArrangement.fourDeckHorizontal.stripDecks == [1, 2, 3, 4])
        #expect(DeckArrangement(rawValue: "Browse") == nil)
    }

    @Test func twoDeckHorizontalIsUnchanged() throws {
        let layout = try Self.layout("ax-7.2.19-2deck-horizontal-parked.json")

        #expect(layout.arrangement == .twoDeckHorizontal)
        #expect(layout.isSupportedPerformanceLayout)
        #expect(layout.stackedWaveform == CGRect(x: 0, y: 136, width: 1492, height: 138))
        #expect(layout.decks.map(\.number) == [1, 2])
        #expect(layout.decks.map(\.waveformStrip) == [CGRect(x: 0, y: 136, width: 1492, height: 69), CGRect(x: 0, y: 205, width: 1492, height: 69)])
        #expect(layout.decks.allSatisfy { $0.strip?.orientation == .horizontal && $0.strip?.markerArea == $0.waveformStrip })
        #expect(layout.decks.allSatisfy { $0.barsText == nil })
        #expect(layout.decks.map(\.overviewFrame) == [CGRect(x: 2, y: 340, width: 686, height: 35), CGRect(x: 804, y: 340, width: 686, height: 35)])
    }

    @Test func twoDeckVerticalHasTwoColumnsUnderTheButtonBar() throws {
        let layout = try Self.layout("ax-7.2.19-2deck-vertical.json")

        #expect(layout.mode == .performance)
        #expect(layout.arrangement == .twoDeckVertical)
        #expect(layout.isSupportedPerformanceLayout)
        #expect(layout.stackedWaveform == CGRect(x: 622, y: 136, width: 248, height: 140))
        #expect(layout.decks.map(\.number) == [1, 2])
        let deck1 = layout.decks[0], deck2 = layout.decks[1]
        #expect(deck1.waveformStrip == CGRect(x: 622, y: 136, width: 124, height: 140))
        #expect(deck2.waveformStrip == CGRect(x: 746, y: 136, width: 124, height: 140))
        #expect(deck1.strip?.orientation == .vertical)
        #expect(deck1.strip?.markerArea == CGRect(x: 622, y: 164, width: 124, height: 112))
        #expect(deck1.strip?.center == 206)
        #expect(deck1.barsText == "184.4Bars")
        #expect(deck2.barsText == "144.4Bars")
        #expect(deck1.masterButtonFrame == CGRect(x: 541, y: 168, width: 70, height: 13))
        #expect(deck2.masterButtonFrame == CGRect(x: 1414, y: 168, width: 70, height: 13))
        #expect(deck1.headerBPMFrame == CGRect(x: 130, y: 166, width: 40, height: 12))
        #expect(deck2.headerBPMFrame == CGRect(x: 1003, y: 166, width: 40, height: 12))
        #expect(deck1.jogBPMFrame == CGRect(x: 467, y: 316, width: 70, height: 26))
        #expect(deck2.jogBPMFrame == CGRect(x: 963, y: 316, width: 70, height: 26))
        #expect(deck1.pitchFrame == CGRect(x: 447, y: 344, width: 58, height: 15))
        #expect(deck1.overviewFrame == CGRect(x: 2, y: 195, width: 615, height: 36))
        #expect(deck2.overviewFrame == CGRect(x: 875, y: 195, width: 615, height: 36))
        #expect(deck1.bpm == 135 && deck2.bpm == 124)
        #expect(deck1.title == "techniuto" && deck2.title == "progre leave a trace")
        #expect(deck1.elapsedSeconds == 327.1)
    }

    @Test func fourDeckHorizontalStacksFourStripsAndNumbersTheQuadrants() throws {
        let layout = try Self.layout("ax-7.2.19-4deck-horizontal.json")

        #expect(layout.arrangement == .fourDeckHorizontal)
        #expect(layout.isSupportedPerformanceLayout)
        #expect(layout.stackedWaveform == CGRect(x: 0, y: 136, width: 1492, height: 194))
        #expect(layout.decks.map(\.number) == [1, 2, 3, 4])
        #expect(layout.decks.map(\.waveformStrip) == [136, 184, 233, 281].map { CGRect(x: 12, y: $0, width: 1468, height: 48) })
        #expect(layout.decks.allSatisfy { $0.strip?.orientation == .horizontal && $0.strip?.center == 746 })
        #expect(layout.decks.map(\.masterButtonFrame) == [
            CGRect(x: 570, y: 361, width: 70, height: 13), CGRect(x: 1414, y: 361, width: 70, height: 13),
            CGRect(x: 570, y: 559, width: 70, height: 13), CGRect(x: 1414, y: 559, width: 70, height: 13),
        ])
        let deck1 = layout.decks[0], deck2 = layout.decks[1]
        #expect(deck1.title == "techniuto" && deck1.bpm == 135)
        #expect(deck2.title == "progre leave a trace" && deck2.bpm == 124)
        #expect(deck1.headerBPMFrame == CGRect(x: 158, y: 359, width: 44, height: 12))
        #expect(deck1.jogBPMFrame == CGRect(x: 523, y: 454, width: 54, height: 23))
        #expect(deck2.jogBPMFrame == CGRect(x: 927, y: 454, width: 54, height: 23))
        #expect(deck1.pitchFrame == CGRect(x: 513, y: 475, width: 35, height: 11))
        #expect(deck2.pitchFrame == CGRect(x: 917, y: 475, width: 35, height: 11))
        #expect(deck1.elapsedSeconds == 327.1)
        #expect(deck2.elapsedSeconds == 278.7)
        #expect(layout.decks.map(\.overviewFrame) == [
            CGRect(x: 2, y: 384, width: 644, height: 26), CGRect(x: 846, y: 384, width: 644, height: 26),
            CGRect(x: 2, y: 582, width: 644, height: 26), CGRect(x: 846, y: 582, width: 644, height: 26),
        ])
    }

    @Test func emptyDecksInFourDeckLayoutsHaveNothingToCover() throws {
        for name in ["ax-7.2.19-4deck-horizontal.json", "ax-7.2.19-4deck-vertical.json"] {
            let empty = try Self.layout(name).decks.filter { $0.number >= 3 }
            #expect(empty.count == 2)
            #expect(empty.allSatisfy { $0.title == nil && $0.bpm == nil && $0.headerBPMFrame == nil && $0.jogBPMFrame == nil && $0.pitchFrame == nil })
            #expect(empty.allSatisfy { $0.elapsedSeconds == 0 && $0.remainingSeconds == 0 })
        }
    }

    @Test func fourDeckVerticalOrdersItsColumnsThreeOneTwoFour() throws {
        let layout = try Self.layout("ax-7.2.19-4deck-vertical.json")

        #expect(layout.arrangement == .fourDeckVertical)
        #expect(layout.isSupportedPerformanceLayout)
        #expect(layout.stackedWaveform == CGRect(x: 622, y: 154, width: 248, height: 160))
        #expect(layout.decks.map(\.number) == [1, 2, 3, 4])
        let strips = Dictionary(uniqueKeysWithValues: layout.decks.map { ($0.number, $0.waveformStrip) })
        #expect(strips[3] == CGRect(x: 622, y: 154, width: 62, height: 160))
        #expect(strips[1] == CGRect(x: 684, y: 154, width: 62, height: 160))
        #expect(strips[2] == CGRect(x: 746, y: 154, width: 62, height: 160))
        #expect(strips[4] == CGRect(x: 808, y: 154, width: 62, height: 160))
        #expect(layout.decks.allSatisfy { $0.strip?.orientation == .vertical && $0.strip?.center == 234 })
        #expect(layout.decks.first { $0.number == 1 }?.strip?.markerArea == CGRect(x: 684, y: 164, width: 62, height: 150))
        #expect(layout.decks.map(\.barsText) == ["184.4Bars", "144.4Bars", nil, nil])
        #expect(layout.decks.map(\.masterButtonFrame.origin) == [CGPoint(x: 541, y: 168), CGPoint(x: 1414, y: 168), CGPoint(x: 541, y: 384), CGPoint(x: 1414, y: 384)])
        let deck1 = layout.decks[0], deck2 = layout.decks[1]
        #expect(deck1.headerBPMFrame == CGRect(x: 144, y: 166, width: 44, height: 12))
        #expect(deck2.headerBPMFrame == CGRect(x: 1017, y: 166, width: 44, height: 12))
        #expect(deck1.jogBPMFrame == CGRect(x: 494, y: 275, width: 54, height: 23))
        #expect(deck2.jogBPMFrame == CGRect(x: 956, y: 275, width: 54, height: 23))
        #expect(deck1.pitchFrame == CGRect(x: 484, y: 296, width: 35, height: 11))
        #expect(layout.decks.map(\.overviewFrame?.minY) == [195, 195, 411, 411])
    }

    @Test func theLiveHeaderBPMRefreshWorksForEveryDeck() throws {
        let snapshot = try Fixture.snapshot("ax-7.2.19-4deck-vertical.json")
        var layout = RekordboxLayout.locate(in: snapshot)
        let indices = layout.headerBPMElementIndices(in: snapshot)

        #expect(indices.keys.sorted() == [1, 2])
        layout.applyLiveHeaderBPM([1: "136.40", 2: "124.00"])
        #expect(layout.decks.map(\.bpm) == [136.4, 124, nil, nil])
    }

    @Test(arguments: ["ax-7.2.19-2deck-vertical.json", "ax-7.2.19-4deck-horizontal.json", "ax-7.2.19-4deck-vertical.json"])
    func everySupportedLayoutActivates(name: String) throws {
        let layout = try Self.layout(name)
        #expect(OverlayActivation.evaluate(isTrusted: true, isRunning: true, layout: layout, isFrontmost: true) == .active)
    }

    @Test func aLayoutWithoutItsWaveformAnchorsWaitsForTheWaveform() throws {
        var snapshot = try Fixture.snapshot("ax-7.2.19-4deck-vertical.json")
        snapshot.elements.removeAll { $0.help?.hasPrefix("Capture") == true }
        let layout = RekordboxLayout.locate(in: snapshot)

        #expect(layout.stackedWaveform == nil)
        #expect(layout.decks.count == 4)
        #expect(!layout.isSupportedPerformanceLayout)
        #expect(OverlayActivation.evaluate(isTrusted: true, isRunning: true, layout: layout, isFrontmost: true) == .decksNotFound)
    }

    @Test func anUnknownLayoutAsksForASupportedOne() throws {
        var snapshot = try Fixture.snapshot("ax-7.2.19-4deck-horizontal.json")
        let index = try #require(snapshot.elements.firstIndex { $0.value == "4Deck Horizontal" })
        snapshot.elements[index].value = "6Deck Horizontal"
        let layout = RekordboxLayout.locate(in: snapshot)

        #expect(layout.arrangement == nil)
        #expect(!layout.isSupportedPerformanceLayout)
        #expect(OverlayActivation.evaluate(isTrusted: true, isRunning: true, layout: layout, isFrontmost: true) == .wrongLayout("6Deck Horizontal"))
    }
}
