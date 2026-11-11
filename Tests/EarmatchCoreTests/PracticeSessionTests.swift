import Foundation
import Testing
@testable import EarmatchCore

struct PracticeSessionTests {
    private static let origin = Date(timeIntervalSinceReferenceDate: 800_000_000)
    private static let beatMilliseconds = 60_000 / 128.0

    @Test func `a mix reports when the tempos matched and the phase error from then on`() throws {
        let incoming = Motion.approach(from: 126, to: 128, at: 5, offset: 0.02)
        let results = MixScript(duration: 20, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: incoming),
        ]).run()

        let result = try #require(results.only)
        let mix = result.mix
        #expect(mix.outgoingDeck == 1)
        #expect(mix.incomingDeck == 2)
        #expect(mix.startedAt == Self.origin)
        #expect(abs(mix.endedAt.timeIntervalSince(Self.origin) - 20) < 0.1)
        #expect(abs(mix.bothPlayingSeconds - 20) < 0.1)
        let secondsToMatch = try #require(mix.secondsToMatch)
        #expect((4.5 ... 6.5).contains(secondsToMatch))
        let expected = 0.02 * Self.beatMilliseconds
        let mean = try #require(mix.meanPhaseErrorMilliseconds)
        let worst = try #require(mix.worstPhaseErrorMilliseconds)
        #expect(abs(mean - expected) < 0.5)
        #expect(abs(worst - expected) < 0.5)
        #expect(abs(try #require(mix.tempoDriftBPM)) < 0.05)
    }

    @Test func `the worst phase error is the largest absolute error after the match`() throws {
        let nudge = Motion.steady(bpm: 128, offset: 0.01).with(offset: { $0 >= 10 && $0 < 11 ? -0.05 : 0.01 })
        let results = MixScript(duration: 16, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: nudge),
        ]).run()

        let mix = try #require(results.only).mix
        #expect(abs(try #require(mix.worstPhaseErrorMilliseconds) - 0.05 * Self.beatMilliseconds) < 0.5)
        #expect(try #require(mix.meanPhaseErrorMilliseconds) < 0.02 * Self.beatMilliseconds)
    }

    @Test(arguments: [(0.98, -0.02), (0.52, -0.48), (3.7, -0.3), (-0.2, -0.2), (2.25, 0.25)])
    func `the phase error wraps to the nearest beat`(offset: Double, wrapped: Double) throws {
        let results = MixScript(duration: 12, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: .steady(bpm: 128, offset: offset)),
        ]).run()

        let result = try #require(results.only)
        let expected = abs(wrapped) * Self.beatMilliseconds
        #expect(abs(try #require(result.mix.meanPhaseErrorMilliseconds) - expected) < 0.5)
        let signed = try #require(result.trace.samples.last).errorMilliseconds
        #expect(abs(signed - wrapped * Self.beatMilliseconds) < 0.5)
    }

    @Test func `the phase error uses the slave deck's beat length`() throws {
        let results = MixScript(duration: 12, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 120, offset: 0.1)),
            DeckScript(number: 2, motion: .steady(bpm: 128), master: { _ in true }),
        ]).run()

        let result = try #require(results.only)
        #expect(result.mix.outgoingDeck == 2)
        #expect(result.mix.incomingDeck == 1)
        let first = try #require(result.trace.samples.first)
        #expect(first.seconds == 0)
        #expect(abs(first.errorMilliseconds - 50) < 0.01)
    }

    @Test func `matched tempos from the start match at once`() throws {
        let results = MixScript(duration: 12, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: .steady(bpm: 128, offset: 0.01)),
        ]).run()

        #expect(try #require(results.only?.mix.secondsToMatch) < 0.5)
    }

    @Test func `tempos that never match leave the match time and the error empty`() throws {
        let results = MixScript(duration: 12, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: .steady(bpm: 125)),
        ]).run()

        let mix = try #require(results.only).mix
        #expect(mix.secondsToMatch == nil)
        #expect(mix.meanPhaseErrorMilliseconds == nil)
        #expect(mix.worstPhaseErrorMilliseconds == nil)
        #expect(mix.tempoDriftBPM == nil)
    }

    @Test func `noisy measured tempo still matches when the phases advance together`() throws {
        var jitter = SplitMix(seed: 7)
        let noisy = Motion.steady(bpm: 128, offset: 0.01).with(bpm: { _ in 128 + jitter.next(in: -1.5 ... 1.5) })
        let results = MixScript(duration: 14, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: noisy),
        ]).run()

        #expect(try #require(results.only?.mix.secondsToMatch) < 0.5)
    }

    @Test func `tempo drift is the mean tempo difference once matched`() throws {
        let results = MixScript(duration: 30, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: .steady(bpm: 128.05)),
        ]).run()

        let drift = try #require(results.only?.mix.tempoDriftBPM)
        #expect(abs(drift - 0.05) < 0.02)
    }

    @Test func `empty snapshots are a gap, not an error and not the end of the mix`() throws {
        let results = MixScript(duration: 20, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: .steady(bpm: 128, offset: 0.02)),
        ], isEmpty: { $0 >= 8 && $0 < 11 }).run()

        let mix = try #require(results.only).mix
        #expect(abs(mix.bothPlayingSeconds - 17) < 0.2)
        #expect(abs(mix.endedAt.timeIntervalSince(Self.origin) - 20) < 0.1)
        #expect(abs(try #require(mix.worstPhaseErrorMilliseconds) - 0.02 * Self.beatMilliseconds) < 0.5)
    }

    @Test func `a phase that cannot be read is not counted as error`() throws {
        let results = MixScript(duration: 16, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: .steady(bpm: 128, offset: 0.02).with(offset: { $0 >= 8 && $0 < 10 ? 0.4 : 0.02 }),
                       phaseKnown: { !($0 >= 8 && $0 < 10) }),
        ]).run()

        let mix = try #require(results.only).mix
        #expect(abs(try #require(mix.worstPhaseErrorMilliseconds) - 0.02 * Self.beatMilliseconds) < 0.5)
        #expect(abs(mix.bothPlayingSeconds - 16) < 0.2)
    }

    @Test func `a Sync Master swap keeps one mix with the same orientation`() throws {
        let results = MixScript(duration: 20, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { $0 < 10 }),
            DeckScript(number: 2, motion: .steady(bpm: 128, offset: 0.02), master: { $0 >= 10 }),
        ]).run()

        let result = try #require(results.only)
        #expect(result.mix.outgoingDeck == 1)
        #expect(result.mix.incomingDeck == 2)
        #expect(abs(try #require(result.mix.meanPhaseErrorMilliseconds) - 0.02 * Self.beatMilliseconds) < 0.5)
        #expect(result.trace.samples.allSatisfy { $0.errorMilliseconds > 0 })
    }

    @Test func `a pause shorter than a second does not end the mix`() throws {
        let results = MixScript(duration: 20, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: .steady(bpm: 128, offset: 0.02), playing: { !($0 >= 9 && $0 < 9.6) }),
        ]).run()

        let mix = try #require(results.only).mix
        #expect(abs(mix.bothPlayingSeconds - 19.4) < 0.2)
    }

    @Test func `a pause of a second or more ends the mix`() {
        let results = MixScript(duration: 25, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: .steady(bpm: 128, offset: 0.02), playing: { !($0 >= 10 && $0 < 11.5) }),
        ]).run()

        #expect(results.count == 2)
        #expect(results.map { $0.mix.endedAt.timeIntervalSince(Self.origin).rounded() } == [10, 25])
    }

    @Test func `a mix shorter than eight seconds is not recorded`() {
        let results = MixScript(duration: 12, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: .steady(bpm: 128), playing: { $0 >= 2 && $0 < 9.5 }),
        ]).run()

        #expect(results.isEmpty)
    }

    @Test func `a long gap closes the open mix at the last time both decks played`() throws {
        var session = PracticeSession(dateAtTimeZero: Self.origin)
        _ = MixScript(duration: 12, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: .steady(bpm: 128)),
        ], finishes: false).run(&session)
        #expect(session.ingest(.empty, at: 12.05) == nil)
        #expect(session.advance(to: 12 + PracticeSession.gapTolerance - 0.5) == nil)

        let closed = session.advance(to: 12 + PracticeSession.gapTolerance + 0.5)
        let result = try #require(closed)
        #expect(abs(result.mix.endedAt.timeIntervalSince(Self.origin) - 12) < 0.1)
        #expect(session.live == nil)
    }

    @Test func `live status follows the open mix`() throws {
        var session = PracticeSession(dateAtTimeZero: Self.origin)
        #expect(session.live == nil)
        _ = MixScript(duration: 6, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: .steady(bpm: 128, offset: 0.02)),
        ], finishes: false).run(&session)

        let live = try #require(session.live)
        #expect(live.outgoingDeck == 1)
        #expect(live.incomingDeck == 2)
        #expect(abs(live.elapsedSeconds - 6) < 0.1)
        #expect(abs(try #require(live.tempoDifferenceBPM)) < 0.1)
        #expect(live.secondsToMatch != nil)
        #expect(abs(try #require(live.phaseErrorMilliseconds) - 0.02 * Self.beatMilliseconds) < 0.5)
        #expect(!live.isLongEnough)
    }

    @Test func `the trace samples the phase error about ten times a second`() throws {
        let results = MixScript(duration: 20, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: .approach(from: 127, to: 128, at: 4, offset: 0.02)),
        ]).run()

        let result = try #require(results.only)
        let trace = result.trace
        #expect(trace.mixID == result.mix.id)
        #expect((190 ... 205).contains(trace.samples.count))
        #expect(abs(trace.durationSeconds - 20) < 0.1)
        #expect(trace.matchedAtSeconds == result.mix.secondsToMatch)
        #expect(trace.samples.map(\.seconds) == trace.samples.map(\.seconds).sorted())
    }

    @Test func `the replay reads the error between samples and finds the worst matched moment`() throws {
        let trace = PracticeTrace(mixID: UUID(), durationSeconds: 3, matchedAtSeconds: 1, samples: [
            PracticeTraceSample(seconds: 0, errorMilliseconds: -90),
            PracticeTraceSample(seconds: 1, errorMilliseconds: 4),
            PracticeTraceSample(seconds: 2, errorMilliseconds: -12),
            PracticeTraceSample(seconds: 3, errorMilliseconds: 6),
        ])

        #expect(trace.errorMilliseconds(at: -1) == -90)
        #expect(trace.errorMilliseconds(at: 1.5) == -4)
        #expect(trace.errorMilliseconds(at: 2) == -12)
        #expect(trace.errorMilliseconds(at: 9) == 6)
        #expect(try #require(trace.worstSample).seconds == 2)
        #expect(PracticeTrace(mixID: UUID(), durationSeconds: 3, matchedAtSeconds: nil, samples: trace.samples).worstSample == nil)
    }

    @Test func `results survive a JSON round trip`() throws {
        let result = try #require(MixScript(duration: 12, decks: [
            DeckScript(number: 1, motion: .steady(bpm: 128), master: { _ in true }),
            DeckScript(number: 2, motion: .steady(bpm: 128, offset: 0.03)),
        ]).run().only)

        let mix = try JSONDecoder().decode(PracticeMix.self, from: JSONEncoder().encode(result.mix))
        let trace = try JSONDecoder().decode(PracticeTrace.self, from: JSONEncoder().encode(result.trace))
        #expect(mix == result.mix)
        #expect(trace == result.trace)
    }
}

struct Motion {
    var beats: (Double) -> Double
    var bpm: (Double) -> Double

    static func steady(bpm: Double, offset: Double = 0) -> Motion {
        Motion(beats: { bpm / 60 * $0 + offset }, bpm: { _ in bpm })
    }

    static func approach(from start: Double, to target: Double, at time: Double, offset: Double) -> Motion {
        Motion(
            beats: { t in target / 60 * t + offset - (target - start) / 60 * min(t - time, 0) },
            bpm: { t in t < time ? start : target }
        )
    }

    func with(offset: @escaping (Double) -> Double) -> Motion {
        let bpm = bpm
        return Motion(beats: { t in bpm(t) / 60 * t + offset(t) }, bpm: bpm)
    }

    func with(bpm reported: @escaping (Double) -> Double) -> Motion {
        Motion(beats: beats, bpm: reported)
    }
}

struct DeckScript {
    var number: Int
    var motion: Motion
    var master: (Double) -> Bool = { _ in false }
    var playing: (Double) -> Bool = { _ in true }
    var phaseKnown: (Double) -> Bool = { _ in true }
}

struct MixScript {
    var duration: Double
    var rate = 30.0
    var decks: [DeckScript]
    var isEmpty: (Double) -> Bool = { _ in false }
    var finishes = true

    func run() -> [PracticeResult] {
        var session = PracticeSession(dateAtTimeZero: Date(timeIntervalSinceReferenceDate: 800_000_000))
        return run(&session)
    }

    func run(_ session: inout PracticeSession) -> [PracticeResult] {
        var results: [PracticeResult] = []
        var stoppedAt: [Int: Double] = [:]
        let steps = Int((duration * rate).rounded())
        for step in 0 ... steps {
            let time = Double(step) / rate
            if isEmpty(time) {
                if let result = session.ingest(.empty, at: time) { results.append(result) }
                continue
            }
            let snapshot = LiveDecksSnapshot(decks: decks.map { deck in
                let playing = deck.playing(time)
                if playing { stoppedAt[deck.number] = nil } else if stoppedAt[deck.number] == nil { stoppedAt[deck.number] = time }
                let position = deck.motion.beats(stoppedAt[deck.number] ?? time)
                let phase = (position.truncatingRemainder(dividingBy: 4) + 4).truncatingRemainder(dividingBy: 4)
                return LiveDeckSnapshot(number: deck.number, title: nil, isMaster: deck.master(time), meter: nil,
                                        phaseInBar: deck.phaseKnown(time) ? phase : nil,
                                        beatsPerSecond: playing ? deck.motion.bpm(time) / 60 : 0)
            }, framesPerSecond: 60, latencyMilliseconds: 20)
            if let result = session.ingest(snapshot, at: time) { results.append(result) }
        }
        if finishes, let result = session.advance(to: duration + PracticeSession.gapTolerance + 1) { results.append(result) }
        return results
    }
}

struct SplitMix {
    private var state: UInt64

    init(seed: UInt64) { state = seed }

    mutating func next(in range: ClosedRange<Double>) -> Double {
        state &+= 0x9E37_79B9_7F4A_7C15
        var z = state
        z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
        z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
        z ^= z >> 31
        let unit = Double(z >> 11) / Double(1 << 53)
        return range.lowerBound + unit * (range.upperBound - range.lowerBound)
    }
}

extension Array {
    var only: Element? { count == 1 ? first : nil }
}
