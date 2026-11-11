import Foundation

public struct BarPosition: Equatable, Sendable {
    public var bar: Int
    public var beat: Int

    public init(bar: Int, beat: Int) {
        self.bar = bar
        self.beat = beat
    }

    public var label: String { String(format: "%02d.%d", bar, beat) }
}

public enum BarCounter {
    public static func position(elapsedSeconds: Double, bpm: Double, phaseInBar: Double?) -> BarPosition {
        let beats = elapsedSeconds * bpm / 60
        let beatsPerBar = BeatGridDetector.beatsPerBar
        guard let phase = phaseInBar else {
            let bar = (beats / beatsPerBar).rounded(.down)
            let beat = positiveModulo(beats, beatsPerBar).rounded(.down)
            return BarPosition(bar: Int(bar) + 1, beat: Int(beat) + 1)
        }
        let bar = ((beats - phase) / beatsPerBar).rounded()
        return BarPosition(bar: Int(bar) + 1, beat: min(Int(phase), Int(beatsPerBar) - 1) + 1)
    }
}
