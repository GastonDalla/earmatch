import CoreGraphics
import Testing
import EarmatchCore

@Suite struct RekordboxLayoutTests {
    @Test func locatesBothDecksInTwoDeckHorizontalPerformanceView() throws {
        let layout = RekordboxLayout.locate(in: try Fixture.snapshot("ax-7.2.19-perf-loaded.json"))

        #expect(layout.mode == .performance)
        #expect(layout.layoutName == "2Deck Horizontal")
        #expect(layout.isSupportedPerformanceLayout)
        #expect(layout.stackedWaveform == CGRect(x: 0, y: 136, width: 1492, height: 138))
        #expect(layout.decks.map(\.number) == [1, 2])

        let deck1 = layout.decks[0], deck2 = layout.decks[1]
        #expect(deck1.bpm == 135.0)
        #expect(deck2.bpm == 124.0)
        #expect(deck1.headerBPMFrame == CGRect(x: 179, y: 308, width: 44, height: 14))
        #expect(deck2.headerBPMFrame == CGRect(x: 981, y: 308, width: 44, height: 14))
        #expect(deck1.jogBPMFrame == CGRect(x: 558, y: 427, width: 54, height: 23))
        #expect(deck2.jogBPMFrame == CGRect(x: 890, y: 427, width: 54, height: 23))
        #expect(deck1.masterButtonFrame == CGRect(x: 612, y: 311, width: 70, height: 13))
        #expect(deck2.masterButtonFrame == CGRect(x: 1414, y: 311, width: 70, height: 13))
        #expect(deck1.waveformStrip == CGRect(x: 0, y: 136, width: 1492, height: 69))
        #expect(deck2.waveformStrip == CGRect(x: 0, y: 205, width: 1492, height: 69))
        #expect(deck1.title == "techniuto")
        #expect(deck1.elapsedSeconds == 0.0)
        #expect(deck2.remainingSeconds == 278.6)
    }

    @Test func emptyDecksStillActivateButHaveNoBPM() throws {
        let layout = RekordboxLayout.locate(in: try Fixture.snapshot("ax-7.2.19-perf-empty.json"))

        #expect(layout.isSupportedPerformanceLayout)
        #expect(layout.decks.count == 2)
        #expect(layout.decks.allSatisfy { $0.bpm == nil && $0.headerBPMFrame == nil && $0.jogBPMFrame == nil })
        #expect(layout.decks.allSatisfy { $0.title == nil })
    }

    @Test func anArtistMadeOfDigitsIsNotMistakenForTheBPM() throws {
        var snapshot = try Fixture.snapshot("ax-7.2.19-perf-loaded.json")
        let artist = try #require(snapshot.elements.firstIndex { $0.role == "AXStaticText" && $0.frame == CGRect(x: 64, y: 308, width: 100, height: 14) })
        snapshot.elements[artist].value = "112"

        let deck1 = RekordboxLayout.locate(in: snapshot).decks[0]

        #expect(deck1.bpm == 135.0)
        #expect(deck1.headerBPMFrame == CGRect(x: 179, y: 308, width: 44, height: 14))
    }

    @Test func exportModeDoesNotActivate() throws {
        let layout = RekordboxLayout.locate(in: try Fixture.snapshot("ax-7.2.19-export-mode.json"))

        #expect(layout.mode == .export)
        #expect(!layout.isSupportedPerformanceLayout)
        #expect(layout.decks.isEmpty)
        #expect(layout.stackedWaveform == nil)
    }

    @Test func otherLayoutsDoNotActivate() throws {
        var snapshot = try Fixture.snapshot("ax-7.2.19-perf-loaded.json")
        let index = try #require(snapshot.elements.firstIndex { $0.value == "2Deck Horizontal" })
        snapshot.elements[index].value = "Browse"

        let layout = RekordboxLayout.locate(in: snapshot)

        #expect(layout.mode == .performance)
        #expect(layout.layoutName == "Browse")
        #expect(!layout.isSupportedPerformanceLayout)
    }

    @Test func anEmptySnapshotIsUnknownAndInactive() {
        let layout = RekordboxLayout.locate(in: RekordboxWindowSnapshot(windowFrame: CGRect(x: 0, y: 0, width: 800, height: 600), elements: []))

        #expect(layout.mode == .unknown)
        #expect(!layout.isSupportedPerformanceLayout)
    }

    @Test func aLoadedTrackWithoutAnalysedBPMKeepsItsTitle() throws {
        var snapshot = try Fixture.snapshot("ax-7.2.19-perf-loaded.json")
        let bpm = try #require(snapshot.elements.firstIndex { $0.value == "135.00" && $0.role == "AXStaticText" })
        snapshot.elements[bpm].value = nil

        let deck1 = RekordboxLayout.locate(in: snapshot).decks[0]

        #expect(deck1.bpm == nil)
        #expect(deck1.title == "techniuto")
    }

    @Test func liveHeaderTextsRefreshTheBPMBetweenFullReads() throws {
        let snapshot = try Fixture.snapshot("ax-7.2.19-perf-loaded.json")
        var layout = RekordboxLayout.locate(in: snapshot)
        let indices = layout.headerBPMElementIndices(in: snapshot)

        #expect(indices.keys.sorted() == [1, 2])
        #expect(snapshot.elements[try #require(indices[1])].value == "135.00")
        #expect(snapshot.elements[try #require(indices[2])].value == "124.00")

        layout.applyLiveHeaderBPM([1: "136.40", 2: ""])

        #expect(layout.decks[0].bpm == 136.4)
        #expect(layout.decks[1].bpm == nil)
    }

    @Test func aMissingLiveTextKeepsTheBPMAndAnEmptyOneClearsIt() throws {
        var layout = RekordboxLayout.locate(in: try Fixture.snapshot("ax-7.2.19-perf-loaded.json"))

        layout.applyLiveHeaderBPM([2: ""])

        #expect(layout.decks[0].bpm == 135.0)
        #expect(layout.decks[1].bpm == nil)
    }
}

@Suite struct DeckLoadedStateTests {
    @Test func aDeckWithABPMButUnreadableTimesKeepsItsTitle() throws {
        var snapshot = try Fixture.snapshot("ax-7.2.19-perf-loaded.json")
        for index in snapshot.elements.indices where ["-05:27", "00:00"].contains(snapshot.elements[index].value ?? "") {
            snapshot.elements[index].value = "-123:45"
        }
        let deck1 = RekordboxLayout.locate(in: snapshot).decks[0]
        #expect(deck1.bpm == 135.0)
        #expect(deck1.title == "techniuto")
    }
}

@Suite struct BeatSyncJogTests {
    @Test func aSyncedDeckStillHasItsJogBPMLocated() throws {
        let layout = RekordboxLayout.locate(in: try Fixture.snapshot("ax-7.2.19-synced-jog-statictext.json"))
        #expect(layout.decks.count == 2)
        #expect(layout.decks.allSatisfy { $0.jogBPMFrame != nil })
        #expect(layout.decks.first { $0.number == 1 }?.jogBPMFrame == CGRect(x: 558, y: 427, width: 54, height: 23))
    }
}

@Suite struct PitchDisplayTests {
    @Test func locatesThePitchPercentageUnderEachJogWithoutTheTempoRange() throws {
        let layout = RekordboxLayout.locate(in: try Fixture.snapshot("ax-7.2.19-perf-loaded.json"))

        #expect(layout.decks[0].pitchFrame == CGRect(x: 548, y: 450, width: 35, height: 11))
        #expect(layout.decks[1].pitchFrame == CGRect(x: 880, y: 450, width: 35, height: 11))
    }

    @Test func aSyncedDeckStillHasItsPitchLocated() throws {
        let layout = RekordboxLayout.locate(in: try Fixture.snapshot("ax-7.2.19-synced-jog-statictext.json"))

        #expect(layout.decks[0].pitchFrame == CGRect(x: 548, y: 450, width: 34, height: 11))
        #expect(layout.decks[1].pitchFrame == CGRect(x: 880, y: 450, width: 35, height: 11))
    }

    @Test func theTempoRangeStaysOutOfThePitchCoverEvenWithoutItsHelpText() throws {
        var snapshot = try Fixture.snapshot("ax-7.2.19-perf-loaded.json")
        for index in snapshot.elements.indices where snapshot.elements[index].help?.hasPrefix("Tempo Range") == true {
            snapshot.elements[index].help = nil
        }
        let layout = RekordboxLayout.locate(in: snapshot)

        #expect(layout.decks[0].pitchFrame == CGRect(x: 548, y: 450, width: 35, height: 11))
    }

    @Test func emptyDecksHaveNoPitchToCover() throws {
        let layout = RekordboxLayout.locate(in: try Fixture.snapshot("ax-7.2.19-perf-empty.json"))

        #expect(layout.decks.allSatisfy { $0.pitchFrame == nil })
    }
}
