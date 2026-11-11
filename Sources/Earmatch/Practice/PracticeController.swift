import EarmatchCore
import Foundation
import Observation
import QuartzCore

@MainActor
@Observable
final class PracticeController {
    static let livePublishInterval: TimeInterval = 1.0 / 15

    private(set) var live: PracticeLiveStatus?
    private(set) var isMixing = false
    private(set) var overview: PracticeOverview = .empty

    @ObservationIgnored private var session = PracticeSession(dateAtTimeZero: Date())
    @ObservationIgnored private let store: PracticeStore
    @ObservationIgnored private var appliedRevision = 0
    @ObservationIgnored private var lastLivePublish: TimeInterval = -.infinity
    @ObservationIgnored private var gapCheck: Task<Void, Never>?

    nonisolated init(store: PracticeStore = PracticeStore()) {
        self.store = store
    }

    func loadHistory() async {
        guard appliedRevision == 0 else { return }
        apply(await store.load())
    }

    func ingest(_ snapshot: LiveDecksSnapshot, at time: TimeInterval) {
        session.dateAtTimeZero = Date(timeIntervalSinceNow: -time)
        let result = session.ingest(snapshot, at: time)
        if let result { record(result) }
        publishLive(at: time, force: result != nil)
        if snapshot.decks.isEmpty {
            scheduleGapCheck()
        } else if let gapCheck {
            gapCheck.cancel()
            self.gapCheck = nil
        }
    }

    private func record(_ result: PracticeResult) {
        Task { @concurrent [weak self, store] in
            let state = await store.record(result)
            await self?.apply(state)
        }
    }

    private func apply(_ state: PracticeStore.State) {
        guard state.revision > appliedRevision else { return }
        appliedRevision = state.revision
        if state.overview != overview { overview = state.overview }
    }

    private func publishLive(at time: TimeInterval, force: Bool) {
        let next = session.live
        if isMixing != (next != nil) { isMixing = next != nil }
        guard next != live else { return }
        let isTransition = (next == nil) != (live == nil) || next?.isMatched != live?.isMatched
        guard force || isTransition || time - lastLivePublish >= Self.livePublishInterval else { return }
        lastLivePublish = time
        live = next
    }

    private func scheduleGapCheck() {
        guard session.isMixing, gapCheck == nil else { return }
        gapCheck = Task { @concurrent [weak self] in
            try? await Task.sleep(for: .seconds(PracticeSession.gapTolerance + 0.25))
            guard !Task.isCancelled else { return }
            await self?.closeIdleMix()
        }
    }

    private func closeIdleMix() {
        gapCheck = nil
        let now = CACurrentMediaTime()
        session.dateAtTimeZero = Date(timeIntervalSinceNow: -now)
        if let result = session.advance(to: now) { record(result) }
        publishLive(at: now, force: true)
    }
}
