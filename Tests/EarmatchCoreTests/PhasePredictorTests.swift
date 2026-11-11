import Testing
import EarmatchCore

struct MeterSession {
    static let frameInterval = 1.0 / 110
    static let refresh = 1.0 / 120
    static let pipelineLatency = 0.006
    static let spacing = 30.0

    struct Drawn {
        let time: Double
        let shown: Double?
        let truth: Double?
    }

    var tracker = PhaseTracker()
    var predictor = PhasePredictor()
    private var nextFrame = 0.0
    private var clock = 0.0

    mutating func run(until end: Double, truth: (Double) -> Double?, captures: (Double) -> Bool = { _ in true }) -> [Drawn] {
        var drawn: [Drawn] = []
        while clock + Self.refresh <= end + 1e-9 {
            clock += Self.refresh
            while nextFrame <= clock - Self.pipelineLatency {
                let time = nextFrame
                nextFrame += Self.frameInterval
                guard captures(time) else { continue }
                let reading = truth(time).map {
                    BeatGridReading(beatSpacing: Self.spacing, beatXs: [], downbeatXs: [], phaseInBar: quantized($0))
                }
                let estimate = tracker.update(reading, at: time)
                predictor.update(PhasePredictor.Anchor(
                    time: time, phaseInBar: estimate.phaseInBar,
                    beatsPerSecond: tracker.beatsPerSecond, isMeasured: estimate.status == .live
                ))
            }
            let target = clock + Self.refresh
            drawn.append(Drawn(time: target, shown: predictor.phase(at: target), truth: truth(target)))
        }
        return drawn
    }
}

func wrapped(_ beats: Double) -> Double {
    let rest = beats.truncatingRemainder(dividingBy: 4)
    return rest < 0 ? rest + 4 : rest
}

func difference(_ a: Double, _ b: Double) -> Double { wrapped(a - b + 2) - 2 }

func quantized(_ phase: Double) -> Double {
    let quantum = 0.5 / MeterSession.spacing
    return wrapped((phase / quantum).rounded() * quantum)
}

@Suite struct PhasePredictorTests {
    let rate = 2.25
    var expectedStep: Double { rate * MeterSession.refresh }

    func steps(_ drawn: [MeterSession.Drawn]) throws -> [Double] {
        let shown = try drawn.map { try #require($0.shown) }
        return zip(shown.dropFirst(), shown).map { difference($0, $1) }
    }

    @Test func aSteadyTempoIsDrawnWhereRekordboxIsWhenTheRefreshReachesTheScreen() throws {
        var session = MeterSession()
        let drawn = session.run(until: 1.0, truth: { wrapped(1 + rate * $0) })

        let settled = drawn.filter { $0.time > 0.2 }
        for frame in settled {
            #expect(abs(difference(try #require(frame.shown), try #require(frame.truth))) < 0.02)
        }
        #expect(try steps(settled).allSatisfy { $0 > 0.5 * expectedStep && $0 < 1.5 * expectedStep })
    }

    @Test func aPausedDeckStopsWhereRekordboxStopped() throws {
        var session = MeterSession()
        let paused = 1 + rate * 0.5
        let drawn = session.run(until: 1.0, truth: { wrapped(1 + rate * min($0, 0.5)) })

        for frame in drawn where frame.time > 0.6 {
            #expect(abs(difference(try #require(frame.shown), paused)) < 0.01)
        }
        for frame in drawn {
            #expect(difference(try #require(frame.shown), try #require(frame.truth)) < 0.15)
        }
        #expect(try steps(drawn.filter { $0.time > 0.2 && $0.time < 0.5 }).allSatisfy { $0 > 0 })
    }

    @Test func aResumedDeckCatchesUpWithinAFewFramesAndNeverStepsBack() throws {
        var session = MeterSession()
        let drawn = session.run(until: 1.0, truth: { $0 < 0.4 ? 2 : wrapped(2 + rate * ($0 - 0.4)) })

        for frame in drawn where frame.time > 0.5 {
            #expect(abs(difference(try #require(frame.shown), try #require(frame.truth))) < 0.03)
        }
        #expect(try steps(drawn).allSatisfy { $0 >= 0 })
    }

    @Test func aSeekIsShownOnceTheTrackerAcceptsIt() throws {
        var session = MeterSession()
        let drawn = session.run(until: 1.0, truth: { wrapped(1 + rate * $0 + ($0 < 0.5 ? 0 : 1.5)) })

        let beforeAccept = drawn.filter { $0.time > 0.2 && $0.time < 0.53 }
        for frame in beforeAccept {
            #expect(abs(difference(try #require(frame.shown), wrapped(1 + rate * frame.time))) < 0.02)
        }
        #expect(try steps(beforeAccept).allSatisfy { $0 > 0.5 * expectedStep && $0 < 1.5 * expectedStep })
        for frame in drawn where frame.time > 0.56 {
            #expect(abs(difference(try #require(frame.shown), try #require(frame.truth))) < 0.03)
        }
    }

    @Test func aBackwardSeekIsShownToo() throws {
        var session = MeterSession()
        let drawn = session.run(until: 1.0, truth: { wrapped(1 + rate * $0 - ($0 < 0.5 ? 0 : 1.0)) })

        for frame in drawn where frame.time > 0.56 {
            #expect(abs(difference(try #require(frame.shown), try #require(frame.truth))) < 0.03)
        }
    }

    @Test func theMeterWrapsPastTheFourthBeatWithoutSteppingBack() throws {
        var session = MeterSession()
        let drawn = session.run(until: 0.5, truth: { wrapped(3.5 + rate * $0) })
        let shown = try drawn.map { try #require($0.shown) }

        #expect(shown.allSatisfy { $0 >= 0 && $0 < 4 })
        #expect(shown.contains { $0 > 3.9 } && shown.contains { $0 < 0.1 })
        #expect(try steps(drawn).allSatisfy { $0 >= 0 })
        #expect(try steps(drawn.filter { $0.time > 0.15 }).allSatisfy { $0 > 0.5 * expectedStep && $0 < 1.5 * expectedStep })
        for frame in drawn where frame.time > 0.2 {
            #expect(abs(difference(try #require(frame.shown), try #require(frame.truth))) < 0.02)
        }
    }

    @Test func aHeldReadingKeepsAdvancingAtTheMeasuredTempo() throws {
        var session = MeterSession()
        let drawn = session.run(until: 1.0, truth: { (0.5 ... 0.6).contains($0) ? nil : wrapped(1 + rate * $0) })

        let held = drawn.filter { $0.time > 0.45 && $0.time < 0.75 }
        #expect(try steps(held).allSatisfy { $0 > 0.5 * expectedStep && $0 < 1.5 * expectedStep })
        for frame in held {
            #expect(abs(difference(try #require(frame.shown), wrapped(1 + rate * frame.time))) < 0.03)
        }
    }

    @Test func aShortCaptureGapKeepsTheMeterMovingAtTheMeasuredTempo() throws {
        var session = MeterSession()
        let drawn = session.run(until: 1.0, truth: { wrapped(1 + rate * $0) }, captures: { !(0.5 ... 0.65).contains($0) })

        let around = drawn.filter { $0.time > 0.45 && $0.time < 0.8 }
        #expect(try steps(around).allSatisfy { $0 > 0.5 * expectedStep && $0 < 1.5 * expectedStep })
        for frame in around {
            #expect(abs(difference(try #require(frame.shown), try #require(frame.truth))) < 0.03)
        }
    }

    @Test func aLongCaptureGapShowsNoGridUntilFramesReturn() throws {
        var session = MeterSession()
        let drawn = session.run(until: 1.2, truth: { wrapped(1 + rate * $0) }, captures: { !(0.5 ... 0.9).contains($0) })

        #expect(drawn.filter { $0.time > 0.5 && $0.time < 0.75 }.allSatisfy { $0.shown != nil })
        #expect(drawn.filter { $0.time > 0.83 && $0.time < 0.9 }.allSatisfy { $0.shown == nil })
        for frame in drawn where frame.time > 1.0 {
            #expect(abs(difference(try #require(frame.shown), try #require(frame.truth))) < 0.03)
        }
    }

    @Test func aLostGridShowsNothing() {
        var predictor = PhasePredictor()
        predictor.update(PhasePredictor.Anchor(time: 0, phaseInBar: 1, beatsPerSecond: 2, isMeasured: true))
        predictor.update(PhasePredictor.Anchor(time: 0.01, phaseInBar: nil, beatsPerSecond: nil, isMeasured: false))

        #expect(predictor.phase(at: 0.02) == nil)
    }
}
