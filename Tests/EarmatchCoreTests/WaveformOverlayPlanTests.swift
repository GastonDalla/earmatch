import CoreGraphics
import Testing
import EarmatchCore

@Suite struct WaveformOverlayPlanTests {
    static let band = CGSize(width: 380, height: 34)

    static func plan(_ fixture: String, _ mode: WaveformMode, meters: Bool = true) throws -> WaveformOverlayPlan {
        let layout = RekordboxLayout.locate(in: try Fixture.snapshot(fixture))
        return try #require(WaveformOverlayPlan(layout: layout, mode: mode, showsMeters: meters, bandSize: band))
    }

    @Test func twoDeckHorizontalSplitKeepsItsTwoHalves() throws {
        let plan = try Self.plan("ax-7.2.19-perf-loaded.json", .split)

        #expect(plan.panel == CGRect(x: 0, y: 136, width: 1492, height: 138))
        #expect(plan.panes.map(\.deck) == [1, 2])
        #expect(plan.panes.map(\.frame) == [CGRect(x: 0, y: 0, width: 745, height: 138), CGRect(x: 747, y: 0, width: 745, height: 138)])
        #expect(plan.panes.map(\.waveformHeight) == [69, 69])
        #expect(plan.panes.map(\.source) == [CGRect(x: 373.5, y: 136, width: 745, height: 69), CGRect(x: 373.5, y: 205, width: 745, height: 69)])
        #expect(plan.panes.allSatisfy { $0.showsMeter })
        #expect(plan.dividers == [CGRect(x: 745, y: 0, width: 2, height: 138)])
        #expect(plan.allowsMasterWaveform)
        #expect(plan.sideMeters.isEmpty)
    }

    @Test func twoDeckHorizontalNormalPutsABandOnTopOfEachStrip() throws {
        let plan = try Self.plan("ax-7.2.19-perf-loaded.json", .normal)

        #expect(plan.panel == CGRect(x: 0, y: 136, width: 1492, height: 138))
        #expect(plan.panes.isEmpty)
        #expect(plan.bands == [1: CGRect(x: 556, y: 0, width: 380, height: 34), 2: CGRect(x: 556, y: 69, width: 380, height: 34)])
    }

    @Test func normalWithoutMetersCoversNothing() throws {
        let layout = RekordboxLayout.locate(in: try Fixture.snapshot("ax-7.2.19-perf-loaded.json"))
        #expect(WaveformOverlayPlan(layout: layout, mode: .normal, showsMeters: false, bandSize: Self.band) == nil)
    }

    @Test func fourDeckHorizontalSplitsIntoAQuadrantPerDeck() throws {
        let plan = try Self.plan("ax-7.2.19-4deck-horizontal.json", .split)

        #expect(plan.panel == CGRect(x: 0, y: 136, width: 1492, height: 194))
        #expect(plan.panes.map(\.deck) == [1, 2, 3, 4])
        #expect(plan.panes.map(\.frame) == [
            CGRect(x: 0, y: 0, width: 745, height: 96), CGRect(x: 747, y: 0, width: 745, height: 96),
            CGRect(x: 0, y: 98, width: 745, height: 96), CGRect(x: 747, y: 98, width: 745, height: 96),
        ])
        #expect(plan.panes.map(\.waveformHeight) == [48, 48, 48, 48])
        #expect(plan.panes.map(\.source.minY) == [136, 184, 233, 281])
        #expect(plan.panes.allSatisfy { $0.source.midX == 746 && $0.source.width == 745 })
        #expect(plan.dividers == [CGRect(x: 745, y: 0, width: 2, height: 194), CGRect(x: 0, y: 96, width: 1492, height: 2)])
    }

    @Test func fourDeckHorizontalBandsLeaveMostOfEachStripVisible() throws {
        let plan = try Self.plan("ax-7.2.19-4deck-horizontal.json", .normal)

        #expect(plan.bands.keys.sorted() == [1, 2, 3, 4])
        #expect(plan.bands[1] == CGRect(x: 556, y: 0, width: 380, height: 28.8))
        #expect(plan.bands[4] == CGRect(x: 556, y: 145, width: 380, height: 28.8))
    }

    @Test(arguments: ["ax-7.2.19-2deck-vertical.json", "ax-7.2.19-4deck-vertical.json"])
    func verticalSplitLeavesTheColumnsAsTheyAre(fixture: String) throws {
        let plan = try Self.plan(fixture, .split)

        #expect(plan.panel == nil)
        #expect(plan.panes.isEmpty && plan.bands.isEmpty && plan.dividers.isEmpty)
        #expect(!plan.allowsMasterWaveform)
    }

    @Test func verticalHideBothBlacksOutEveryColumn() throws {
        let plan = try Self.plan("ax-7.2.19-4deck-vertical.json", .hideBoth)

        #expect(plan.panel == CGRect(x: 622, y: 154, width: 248, height: 160))
        #expect(plan.panes.map(\.deck) == [1, 2, 3, 4])
        #expect(plan.panes.map(\.frame) == [
            CGRect(x: 62, y: 0, width: 62, height: 160), CGRect(x: 124, y: 0, width: 62, height: 160),
            CGRect(x: 0, y: 0, width: 62, height: 160), CGRect(x: 186, y: 0, width: 62, height: 160),
        ])
        #expect(plan.panes.allSatisfy { !$0.showsMeter && $0.waveformHeight == 0 })
    }

    @Test func verticalMetersSitOnEachDecksOverviewNextToTheColumns() throws {
        let plan = try Self.plan("ax-7.2.19-4deck-vertical.json", .normal)

        #expect(plan.sideMeters == [
            1: CGRect(x: 237, y: 196, width: 380, height: 34), 2: CGRect(x: 875, y: 196, width: 380, height: 34),
            3: CGRect(x: 237, y: 412, width: 380, height: 34), 4: CGRect(x: 875, y: 412, width: 380, height: 34),
        ])
        let withoutMeters = RekordboxLayout.locate(in: try Fixture.snapshot("ax-7.2.19-4deck-vertical.json"))
        #expect(WaveformOverlayPlan(layout: withoutMeters, mode: .normal, showsMeters: false, bandSize: Self.band) == nil)
    }

    @Test func twoDeckVerticalMetersFlankTheColumns() throws {
        let plan = try Self.plan("ax-7.2.19-2deck-vertical.json", .hideBoth)

        #expect(plan.panel == CGRect(x: 622, y: 136, width: 248, height: 140))
        #expect(plan.panes.map(\.frame) == [CGRect(x: 0, y: 0, width: 124, height: 140), CGRect(x: 124, y: 0, width: 124, height: 140)])
        #expect(plan.sideMeters == [1: CGRect(x: 237, y: 196, width: 380, height: 34), 2: CGRect(x: 875, y: 196, width: 380, height: 34)])
    }
}
