import Foundation

struct PracticeDeckTempo: Sendable {
    static let window: TimeInterval = 2
    static let minimumSpan: TimeInterval = 1
    static let minimumSamples = 8
    static let jumpTolerance = 0.25

    private struct Sample: Sendable {
        var time: TimeInterval
        var beats: Double
    }

    private var samples: [Sample] = []
    private var lastPhase: Double?
    private(set) var playingSince: TimeInterval?

    mutating func add(_ deck: LiveDeckSnapshot, at time: TimeInterval) {
        if deck.isPlaying {
            if playingSince == nil { playingSince = time }
        } else if deck.phaseInBar != nil {
            playingSince = nil
        }
        guard deck.isPlaying, let phase = deck.phaseInBar else {
            reset()
            return
        }
        defer { lastPhase = phase }
        guard let last = samples.last, let lastPhase else {
            samples.append(Sample(time: time, beats: phase))
            return
        }
        let elapsed = time - last.time
        guard elapsed > 0 else { return }
        let step = PhaseTracker.circularBeats(phase - lastPhase)
        if elapsed > PracticeSession.maximumSampleInterval || abs(step - deck.beatsPerSecond * elapsed) > Self.jumpTolerance {
            samples.removeAll(keepingCapacity: true)
            samples.append(Sample(time: time, beats: phase))
            return
        }
        samples.append(Sample(time: time, beats: last.beats + step))
        if let firstKept = samples.firstIndex(where: { time - $0.time <= Self.window }), firstKept > 0 {
            samples.removeFirst(firstKept)
        }
    }

    var beatsPerMinute: Double? {
        guard samples.count >= Self.minimumSamples, let first = samples.first, let last = samples.last,
              last.time - first.time >= Self.minimumSpan
        else { return nil }
        let count = Double(samples.count)
        let meanTime = samples.reduce(0) { $0 + ($1.time - first.time) } / count
        let meanBeats = samples.reduce(0) { $0 + $1.beats } / count
        var covariance = 0.0
        var variance = 0.0
        for sample in samples {
            let time = sample.time - first.time - meanTime
            covariance += time * (sample.beats - meanBeats)
            variance += time * time
        }
        guard variance > 0 else { return nil }
        return covariance / variance * 60
    }

    private mutating func reset() {
        samples.removeAll(keepingCapacity: true)
        lastPhase = nil
    }
}
