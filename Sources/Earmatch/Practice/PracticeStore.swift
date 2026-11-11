import EarmatchCore
import Foundation
import os

actor PracticeStore {
    struct State: Sendable {
        var revision: Int
        var overview: PracticeOverview
    }

    static var defaultURL: URL {
        URL.applicationSupportDirectory
            .appending(path: "Earmatch", directoryHint: .isDirectory)
            .appending(path: "practice.json", directoryHint: .notDirectory)
    }

    private let file: PracticeHistoryFile
    private let calendar: Calendar
    private let logger = Logger(subsystem: Bundle.main.bundleIdentifier ?? "local.earmatch", category: "practice")
    private var history: PracticeHistory?
    private var revision = 0

    init(url: URL = PracticeStore.defaultURL, calendar: Calendar = .autoupdatingCurrent) {
        file = PracticeHistoryFile(url: url)
        self.calendar = calendar
    }

    func load() -> State {
        state(of: loadedHistory())
    }

    func record(_ result: PracticeResult) -> State {
        var history = loadedHistory()
        history.record(result)
        self.history = history
        do {
            try file.save(history)
        } catch {
            logger.error("Practice history not saved: \(error.localizedDescription, privacy: .public)")
        }
        return state(of: history)
    }

    private func loadedHistory() -> PracticeHistory {
        if let history { return history }
        let loaded = file.load()
        if let problem = loaded.problem {
            logger.error("Practice history unreadable, starting fresh: \(problem, privacy: .public)")
        }
        if let kept = loaded.quarantinedURL {
            logger.notice("Unreadable practice history kept as \(kept.lastPathComponent, privacy: .public)")
        }
        history = loaded.history
        return loaded.history
    }

    private func state(of history: PracticeHistory) -> State {
        revision += 1
        return State(revision: revision, overview: PracticeOverview(history: history, calendar: calendar))
    }
}
