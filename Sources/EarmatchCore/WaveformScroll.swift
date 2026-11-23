import Foundation

public enum WaveformScroll {
    public static let maxShiftBeats = PhaseTracker.phaseTolerance

    public static func shift(framePhase: Double?, drawnPhase: Double?, beatSpacing: Double?, pixelsPerUnit: Double) -> Double {
        guard let framePhase, let drawnPhase, let beatSpacing, beatSpacing > 0 else { return 0 }
        let beats = PhaseTracker.circularBeats(drawnPhase - framePhase)
        guard abs(beats) <= maxShiftBeats else { return 0 }
        return (beats * beatSpacing * pixelsPerUnit).rounded()
    }
}
