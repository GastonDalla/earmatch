import Foundation
import EarmatchCore

final class LiveDecksModel: ObservableObject {
    @Published private(set) var snapshot: LiveDecksSnapshot?
    private var receivedAt: TimeInterval = 0

    func apply(_ snapshot: LiveDecksSnapshot, at time: TimeInterval) {
        receivedAt = time
        if snapshot != self.snapshot { self.snapshot = snapshot }
    }

    func clear() {
        if snapshot != nil { snapshot = nil }
    }

    func expire(olderThan age: TimeInterval, now: TimeInterval) {
        if snapshot != nil, now - receivedAt > age { snapshot = nil }
    }

    var decks: [LiveDeckSnapshot] {
        (snapshot?.decks ?? []).sorted { $0.number < $1.number }
    }

    var previewMeter: PhaseMeterDisplay? {
        let decks = decks
        return decks.first { $0.meter?.masterPlayer != nil }?.meter ?? decks.first { $0.meter != nil }?.meter
    }

    var waveformPhases: DeckPhases? {
        func phase(_ number: Int) -> Double? {
            guard let row = decks.first(where: { $0.number == number })?.meter?.rows.first(where: { $0.role == .deck }),
                  let beat = row.beatInBar
            else { return nil }
            return Double(beat) + (row.fraction ?? 0)
        }
        guard let one = phase(1), let two = phase(2) else { return nil }
        return DeckPhases(one: one, two: two)
    }
}
