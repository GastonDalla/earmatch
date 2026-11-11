import Foundation
import EarmatchCore

struct PreviewClock {
    static let bpm = 128.0
    private static let cycle = 12.0
    private static let nudgeDuration = 0.45

    let seconds: Double

    init(date: Date) {
        seconds = date.timeIntervalSinceReferenceDate.truncatingRemainder(dividingBy: 60)
    }

    var masterBeats: Double { 20 + seconds * Self.bpm / 60 }

    var deckBeats: Double {
        let wobble = 0.07 * sin(seconds * 2 * .pi / 3.7)
        return masterBeats + 72 + nudge + wobble
    }

    func meterDisplay(style: PhaseMeterType) -> PhaseMeterDisplay {
        PhaseMeterModel.display(for: 1, decks: [Self.input(1, beats: deckBeats), Self.input(2, beats: masterBeats)], master: 2, style: style)
    }

    private static func input(_ number: Int, beats: Double) -> PhaseMeterDeckInput {
        let bars = (beats / 4).rounded(.down)
        let phase = beats - 4 * bars
        return PhaseMeterDeckInput(number: number, phaseInBar: phase, counter: BarPosition(bar: Int(bars) + 1, beat: Int(phase) + 1))
    }

    private var nudge: Double {
        let t = seconds.truncatingRemainder(dividingBy: Self.cycle)
        let half = Self.cycle / 2
        func ease(_ x: Double) -> Double {
            let clamped = min(max(x, 0), 1)
            return clamped * clamped * (3 - 2 * clamped)
        }
        if t < half { return 1 - ease(t / Self.nudgeDuration) }
        return ease((t - half) / Self.nudgeDuration)
    }
}
