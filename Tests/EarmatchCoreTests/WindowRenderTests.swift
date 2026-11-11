import Foundation
import Testing
import EarmatchCore

@Suite struct WindowRenderRequestTests {
    @Test func withoutTheFlagTheAppStartsNormally() throws {
        #expect(try WindowRenderRequest(arguments: ["Earmatch"]) == nil)
        #expect(try WindowRenderRequest(arguments: ["Earmatch", "--dump-ax"]) == nil)
    }

    @Test func theFlagNamesTheOutputDirectoryAndDefaultsToTheDefaultWindowSize() throws {
        let request = try #require(try WindowRenderRequest(arguments: ["Earmatch", "--render-window", "/tmp/renders"]))
        #expect(request.directory == URL(fileURLWithPath: "/tmp/renders", isDirectory: true))
        #expect(request.size == MainWindowMetrics.defaultSize)
    }

    @Test func theMinimumSizeCanBeRequested() throws {
        let request = try #require(try WindowRenderRequest(arguments: ["Earmatch", "--render-window", "out", "--render-size", "minimum"]))
        #expect(request.size == MainWindowMetrics.minimumSize)
    }

    @Test func anExplicitSizeIsReadAsWidthByHeight() throws {
        let request = try #require(try WindowRenderRequest(arguments: ["Earmatch", "--render-size", "1200x760", "--render-window", "out"]))
        #expect(request.size == CGSize(width: 1200, height: 760))
    }

    @Test func aMissingDirectoryIsAnError() {
        #expect(throws: WindowRenderRequest.Problem.missingDirectory) {
            try WindowRenderRequest(arguments: ["Earmatch", "--render-window"])
        }
        #expect(throws: WindowRenderRequest.Problem.missingDirectory) {
            try WindowRenderRequest(arguments: ["Earmatch", "--render-window", "--render-size", "minimum"])
        }
    }

    @Test(arguments: ["huge", "1200", "x700", "1200x", "0x700", "-5x700", "1200x700x3"])
    func anUnreadableSizeIsAnError(size: String) {
        #expect(throws: WindowRenderRequest.Problem.invalidSize(size)) {
            try WindowRenderRequest(arguments: ["Earmatch", "--render-window", "out", "--render-size", size])
        }
    }

    @Test func aSizeBelowTheMinimumIsAnError() {
        #expect(throws: WindowRenderRequest.Problem.belowMinimum("400x300")) {
            try WindowRenderRequest(arguments: ["Earmatch", "--render-window", "out", "--render-size", "400x300"])
        }
    }

    @Test func everyProblemExplainsTheUsage() {
        let problems: [WindowRenderRequest.Problem] = [.missingDirectory, .invalidSize("huge"), .belowMinimum("400x300")]
        for problem in problems {
            #expect(problem.message.contains("--render-window <directory>"))
        }
    }
}

@Suite struct WindowRenderShotTests {
    @Test func oneRenderPerPanePlusTheFourDeckLiveView() {
        #expect(WindowRenderShot.all.map(\.fileName) == ["live.png", "overlay.png", "practice.png", "setup.png", "live-4deck.png"])
        #expect(WindowRenderShot.all.map(\.pane) == [.live, .overlay, .practice, .setup, .live])
        #expect(WindowRenderShot.all.map(\.deckCount) == [2, 2, 2, 2, 4])
    }

    @Test func rendersAreTwiceThePointSize() {
        #expect(WindowRenderShot.scale == 2)
    }

    @Test func theWindowFitsA13InchMacBook() {
        let usable = CGSize(width: 1440, height: 900 - 25 - 70)
        #expect(MainWindowMetrics.defaultSize.width <= usable.width)
        #expect(MainWindowMetrics.defaultSize.height <= usable.height)
        #expect(MainWindowMetrics.minimumSize.width <= MainWindowMetrics.defaultSize.width)
        #expect(MainWindowMetrics.minimumSize.height <= MainWindowMetrics.defaultSize.height)
        #expect(MainWindowMetrics.sidebarWidth < MainWindowMetrics.minimumSize.width / 3)
    }
}

@Suite struct WindowRenderSampleTests {
    @Test(arguments: [2, 4])
    func theSampleDecksAreLoadedPlayingAndFollowOneMaster(count: Int) throws {
        let snapshot = WindowRenderSample.decks(count: count)
        #expect(snapshot.decks.map(\.number) == Array(1 ... count))
        #expect(snapshot.decks.filter { $0.isMaster }.count == 1)
        #expect(snapshot.decks.allSatisfy { $0.title != nil && $0.meter != nil && $0.isPlaying })
        #expect(snapshot.framesPerSecond > 0)
        #expect(snapshot.latencyMilliseconds > 0)
        let masters = snapshot.decks.filter { $0.isMaster }
        let master = try #require(masters.first)
        for deck in snapshot.decks where !deck.isMaster {
            #expect(deck.meter?.masterPlayer == master.number)
        }
    }

    @Test(arguments: [2, 4])
    func theSampleStatusReadsLiveWithFramesAndLatency(count: Int) {
        let context = WindowRenderSample.statusContext(deckCount: count)
        let line = L10n.english { StatusText.liveDecks(context, snapshot: WindowRenderSample.decks(count: count)) }
        #expect(line.tone == .live)
        #expect(line.text.hasPrefix("Live · "))
        #expect(line.text.hasSuffix(" ms"))
        #expect(context.layoutName == (count == 4 ? "4Deck Horizontal" : "2Deck Horizontal"))
        #expect(StatusText.connection(context).tone == .live)
    }

    @Test func theSampleSettingsAreTheCDJ3000BoothWithEverythingOn() {
        let settings = WindowRenderSample.settings
        #expect(BoothPreset.selected(in: settings) == .cdj3000)
        #expect(settings.isEnabled && settings.isMixTrainerEnabled && settings.isPhaseMeterEnabled && settings.isBPMLimitEnabled)
    }

    @Test func theSamplePracticeHistoryEndsTodayWithAReplayOfTheLastMix() throws {
        let calendar = Calendar(identifier: .gregorian)
        let now = Date(timeIntervalSince1970: 1_800_000_000)
        let overview = WindowRenderSample.practiceOverview(now: now, calendar: calendar)
        let mix = try #require(overview.lastMix)
        let trace = try #require(overview.lastTrace)
        #expect(trace.mixID == mix.id)
        #expect(!trace.samples.isEmpty)
        #expect(trace.matchedAtSeconds != nil)
        #expect(overview.days.count >= 7)
        #expect(overview.mixCount >= overview.days.count)
        #expect(overview.days.last.map { calendar.isDate($0.day, inSameDayAs: now) } == true)
        #expect(mix.endedAt <= now)
    }

    @Test func theSamplePracticeHistoryIsTheSameEveryRun() {
        let calendar = Calendar(identifier: .gregorian)
        let now = Date(timeIntervalSince1970: 1_800_000_000)
        #expect(WindowRenderSample.practiceOverview(now: now, calendar: calendar) == WindowRenderSample.practiceOverview(now: now, calendar: calendar))
    }

    @Test func theSampleLiveMixIsTempoMatchedAndInPhase() throws {
        let live = WindowRenderSample.practiceLive
        #expect(live.isMatched)
        #expect(live.isLongEnough)
        let error = try #require(live.phaseErrorMilliseconds)
        #expect(abs(error) < 20)
    }
}
