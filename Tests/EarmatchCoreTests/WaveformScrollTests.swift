import Testing
import EarmatchCore

struct WaveformSession {
    static let frameInterval = 1.0 / 110
    static let refresh = 1.0 / 120
    static let pipelineLatency = 0.006
    static let spacing = 30.0
    static let pixelsPerPoint = 2.0
    static var pixelsPerBeat: Double { spacing * pixelsPerPoint }

    struct Shown {
        let time: Double
        let pixels: Double
        let rekordbox: Double
    }

    private struct Frame {
        let pixels: Double
        let phase: Double
    }

    var tracker = PhaseTracker()
    var predictor = PhasePredictor()
    private var pending: [(available: Double, frame: Frame)] = []
    private var frame: Frame?
    private var nextCapture = 0.0
    private var clock = 0.0

    static func rekordboxPixels(at time: Double, beats: (Double) -> Double) -> Double {
        let audioBlock = 512.0 / 44_100
        let played = beats((time / audioBlock).rounded(.down) * audioBlock)
        return (played * pixelsPerBeat / pixelsPerPoint).rounded(.down) * pixelsPerPoint
    }

    mutating func run(until end: Double, beats: (Double) -> Double, captures: (Double) -> Bool = { _ in true }) -> [Shown] {
        var shown: [Shown] = []
        while clock + Self.refresh <= end + 1e-9 {
            clock += Self.refresh
            while nextCapture <= clock {
                let time = nextCapture
                nextCapture += Self.frameInterval
                guard captures(time) else { continue }
                let pixels = Self.rekordboxPixels(at: time, beats: beats)
                let phase = wrapped(pixels / Self.pixelsPerBeat)
                let estimate = tracker.update(BeatGridReading(beatSpacing: Self.spacing, beatXs: [], downbeatXs: [], phaseInBar: phase), at: time)
                predictor.update(PhasePredictor.Anchor(time: time, phaseInBar: estimate.phaseInBar,
                                                       beatsPerSecond: tracker.beatsPerSecond, isMeasured: estimate.status == .live))
                pending.append((time + Self.pipelineLatency, Frame(pixels: pixels, phase: phase)))
            }
            while let first = pending.first, first.available <= clock {
                frame = first.frame
                pending.removeFirst()
            }
            let target = clock + Self.refresh
            let drawn = predictor.phase(at: target)
            guard let frame else { continue }
            let shift = WaveformScroll.shift(framePhase: frame.phase, drawnPhase: drawn, beatSpacing: Self.spacing,
                                             pixelsPerUnit: Self.pixelsPerPoint)
            shown.append(Shown(time: target, pixels: frame.pixels + shift, rekordbox: Self.rekordboxPixels(at: target, beats: beats)))
        }
        return shown
    }
}

@Suite struct WaveformScrollTests {
    let rate = 2.25
    var pixelsPerRefresh: Double { rate * WaveformSession.pixelsPerBeat * WaveformSession.refresh }

    func steps(_ shown: [WaveformSession.Shown]) -> [Double] {
        zip(shown.dropFirst(), shown).map { $0.pixels - $1.pixels }
    }

    func errors(_ shown: [WaveformSession.Shown]) -> [Double] {
        shown.map { $0.pixels - $0.rekordbox }
    }

    @Test func aPlayingDeckIsDrawnWhereRekordboxDrawsItWhenTheRefreshReachesTheScreen() {
        var session = WaveformSession()
        let errors = errors(session.run(until: 3.0, beats: { 1 + rate * $0 }).filter { $0.time > 0.3 })

        #expect(abs(errors.reduce(0, +) / Double(errors.count)) <= 0.5)
        #expect(errors.allSatisfy { abs($0) <= WaveformSession.pixelsPerPoint })
    }

    @Test func aPlayingDeckScrollsOnAlmostEveryRefreshWithoutSteppingBackOrSkipping() {
        var session = WaveformSession()
        let playing = steps(session.run(until: 3.0, beats: { 1 + rate * $0 }).filter { $0.time > 0.3 })

        #expect(playing.allSatisfy { $0 >= 0 && $0 <= pixelsPerRefresh.rounded(.up) })
        #expect(Double(playing.filter { $0 == 0 }.count) <= 0.1 * Double(playing.count))
    }

    @Test func aMissedCaptureDoesNotMakeTheWaveformJump() {
        var session = WaveformSession()
        let everyThirtyFifthDropped: (Double) -> Bool = { Int(($0 * 110).rounded()) % 35 != 0 }
        let shown = session.run(until: 3.0, beats: { 1 + rate * $0 }, captures: everyThirtyFifthDropped).filter { $0.time > 0.3 }

        #expect(steps(shown).allSatisfy { $0 >= 0 && $0 <= pixelsPerRefresh.rounded(.up) })
        #expect(errors(shown).allSatisfy { abs($0) <= WaveformSession.pixelsPerPoint })
    }

    @Test func aPausedDeckStaysWhereRekordboxStopped() {
        var session = WaveformSession()
        let stop = 1.0
        let beats: (Double) -> Double = { 1 + rate * min($0, stop) }
        let shown = session.run(until: 2.0, beats: beats)
        let stopped = WaveformSession.rekordboxPixels(at: stop + 0.1, beats: beats)

        #expect(shown.filter { $0.time > stop + 0.2 }.allSatisfy { $0.pixels == stopped })
    }

    @Test func theShiftFollowsTheDrawnPhaseAcrossTheEndOfTheBar() {
        #expect(WaveformScroll.shift(framePhase: 3.98, drawnPhase: 0.02, beatSpacing: 30, pixelsPerUnit: 2) == 2.4.rounded())
        #expect(WaveformScroll.shift(framePhase: 0.02, drawnPhase: 3.98, beatSpacing: 30, pixelsPerUnit: 2) == -2.4.rounded())
    }

    @Test func theShiftIsRoundedToWholeCapturedPixels() {
        #expect(WaveformScroll.shift(framePhase: 1, drawnPhase: 1.01, beatSpacing: 30, pixelsPerUnit: 2) == 1)
        #expect(WaveformScroll.shift(framePhase: 1, drawnPhase: 1.01, beatSpacing: 30, pixelsPerUnit: 1) == 0)
    }

    @Test func aSeekTheMeterHasNotFollowedYetIsNotShifted() {
        #expect(WaveformScroll.shift(framePhase: 1, drawnPhase: 1 + WaveformScroll.maxShiftBeats + 0.01, beatSpacing: 30, pixelsPerUnit: 2) == 0)
        #expect(WaveformScroll.shift(framePhase: 1, drawnPhase: 3, beatSpacing: 30, pixelsPerUnit: 2) == 0)
    }

    @Test func withoutAPhaseOrASpacingTheFrameIsShownAsCaptured() {
        #expect(WaveformScroll.shift(framePhase: nil, drawnPhase: 1, beatSpacing: 30, pixelsPerUnit: 2) == 0)
        #expect(WaveformScroll.shift(framePhase: 1, drawnPhase: nil, beatSpacing: 30, pixelsPerUnit: 2) == 0)
        #expect(WaveformScroll.shift(framePhase: 1, drawnPhase: 1.1, beatSpacing: nil, pixelsPerUnit: 2) == 0)
        #expect(WaveformScroll.shift(framePhase: 1, drawnPhase: 1.1, beatSpacing: 0, pixelsPerUnit: 2) == 0)
    }
}
