import Foundation

public struct PracticeSession: Sendable {
    public static let pauseTolerance: TimeInterval = 1
    public static let gapTolerance: TimeInterval = 10
    public static let minimumMixSeconds: TimeInterval = 8
    public static let matchToleranceBPM = 0.1
    public static let matchHoldSeconds: TimeInterval = 2
    public static let traceInterval: TimeInterval = 0.1
    static let maximumSampleInterval: TimeInterval = 0.25
    static let traceCapacity = 6000

    public var dateAtTimeZero: Date
    public private(set) var live: PracticeLiveStatus?
    private var tempos: [Int: PracticeDeckTempo] = [:]
    private var mix: OpenMix?

    public init(dateAtTimeZero: Date) {
        self.dateAtTimeZero = dateAtTimeZero
    }

    public var isMixing: Bool { mix != nil }

    public mutating func ingest(_ snapshot: LiveDecksSnapshot, at time: TimeInterval) -> PracticeResult? {
        guard !snapshot.decks.isEmpty else { return advance(to: time) }
        for deck in snapshot.decks {
            tempos[deck.number, default: PracticeDeckTempo()].add(deck, at: time)
        }
        var finished: PracticeResult?
        if var open = mix {
            let outgoing = snapshot.decks.first { $0.number == open.outgoing }
            let incoming = snapshot.decks.first { $0.number == open.incoming }
            if let outgoing, let incoming, outgoing.isPlaying, incoming.isPlaying {
                open.observe(outgoing: outgoing, incoming: incoming, tempos: tempos, at: time)
                mix = open
            } else if pair(in: snapshot) != nil {
                finished = close(open)
            } else {
                let isStopped = [outgoing, incoming].contains { deck in
                    deck.map { !$0.isPlaying && $0.phaseInBar != nil } ?? false
                }
                if time - open.lastBothPlaying >= (isStopped ? Self.pauseTolerance : Self.gapTolerance) {
                    finished = close(open)
                } else {
                    open.interrupt()
                    mix = open
                }
            }
        }
        if mix == nil, let next = pair(in: snapshot) {
            var open = OpenMix(outgoing: next.outgoing.number, incoming: next.incoming.number, start: time)
            open.observe(outgoing: next.outgoing, incoming: next.incoming, tempos: tempos, at: time)
            mix = open
        }
        live = mix?.status(at: time)
        return finished
    }

    public mutating func advance(to time: TimeInterval) -> PracticeResult? {
        guard var open = mix else { return nil }
        if time - open.lastBothPlaying >= Self.gapTolerance { return close(open) }
        open.interrupt()
        mix = open
        live = open.status(at: time)
        return nil
    }

    private mutating func close(_ open: OpenMix) -> PracticeResult? {
        mix = nil
        live = nil
        return open.result(dateAtTimeZero: dateAtTimeZero)
    }

    private func pair(in snapshot: LiveDecksSnapshot) -> (outgoing: LiveDeckSnapshot, incoming: LiveDeckSnapshot)? {
        let playing = snapshot.decks.filter(\.isPlaying)
        guard playing.count >= 2 else { return nil }
        func started(_ deck: LiveDeckSnapshot) -> TimeInterval { tempos[deck.number]?.playingSince ?? .infinity }
        let byStart = playing.sorted { (started($0), $0.number) < (started($1), $1.number) }
        let outgoing = playing.first(where: \.isMaster) ?? byStart[0]
        guard let incoming = byStart.last(where: { $0.number != outgoing.number }) else { return nil }
        return (outgoing, incoming)
    }
}

private struct OpenMix: Sendable {
    let id = UUID()
    let outgoing: Int
    let incoming: Int
    let start: TimeInterval
    var lastBothPlaying: TimeInterval
    private var lastSample: TimeInterval?
    private var bothPlayingSeconds = 0.0
    private var candidateStart: TimeInterval?
    private var lastTempoSample: TimeInterval?
    private var matchedAt: TimeInterval?
    private var matched = MatchedStats()
    private var pending = MatchedStats()
    private var tempoDifference: Double?
    private var phaseError: Double?
    private var trace: [PracticeTraceSample] = []
    private var traceInterval = PracticeSession.traceInterval

    init(outgoing: Int, incoming: Int, start: TimeInterval) {
        self.outgoing = outgoing
        self.incoming = incoming
        self.start = start
        lastBothPlaying = start
    }

    mutating func observe(outgoing deck: LiveDeckSnapshot, incoming other: LiveDeckSnapshot,
                          tempos: [Int: PracticeDeckTempo], at time: TimeInterval) {
        if let lastSample, time - lastSample <= PracticeSession.maximumSampleInterval {
            bothPlayingSeconds += time - lastSample
        }
        lastSample = time
        lastBothPlaying = time

        let outgoingTempo = tempos[deck.number]?.beatsPerMinute
        let incomingTempo = tempos[other.number]?.beatsPerMinute
        tempoDifference = if let outgoingTempo, let incomingTempo { incomingTempo - outgoingTempo } else { nil }
        let slaveIsOutgoing = other.isMaster && !deck.isMaster
        let slaveTempo = (slaveIsOutgoing ? outgoingTempo : incomingTempo)
            ?? (slaveIsOutgoing ? deck : other).beatsPerSecond * 60
        phaseError = Self.phaseError(outgoing: deck, incoming: other, slaveBPM: slaveTempo)

        if matchedAt == nil { updateMatch(at: time) }
        if matchedAt != nil {
            matched.add(error: phaseError, tempoDifference: tempoDifference)
        } else if candidateStart != nil {
            pending.add(error: phaseError, tempoDifference: tempoDifference)
        }
        appendTrace(at: time)
    }

    mutating func interrupt() {
        lastSample = nil
        phaseError = nil
        if matchedAt == nil {
            candidateStart = nil
            pending = MatchedStats()
        }
    }

    func status(at time: TimeInterval) -> PracticeLiveStatus {
        PracticeLiveStatus(outgoingDeck: outgoing, incomingDeck: incoming, elapsedSeconds: time - start,
                           bothPlayingSeconds: bothPlayingSeconds, tempoDifferenceBPM: tempoDifference,
                           secondsToMatch: matchedAt.map { $0 - start }, phaseErrorMilliseconds: phaseError,
                           meanPhaseErrorMilliseconds: matched.meanError)
    }

    func result(dateAtTimeZero: Date) -> PracticeResult? {
        guard bothPlayingSeconds >= PracticeSession.minimumMixSeconds else { return nil }
        let secondsToMatch = matchedAt.map { $0 - start }
        let mix = PracticeMix(id: id, startedAt: dateAtTimeZero.addingTimeInterval(start),
                              endedAt: dateAtTimeZero.addingTimeInterval(lastBothPlaying),
                              outgoingDeck: outgoing, incomingDeck: incoming, bothPlayingSeconds: bothPlayingSeconds,
                              secondsToMatch: secondsToMatch, tempoDriftBPM: matched.meanTempoDifference,
                              meanPhaseErrorMilliseconds: matched.meanError, worstPhaseErrorMilliseconds: matched.worstError)
        let trace = PracticeTrace(mixID: id, durationSeconds: lastBothPlaying - start,
                                  matchedAtSeconds: secondsToMatch, samples: trace)
        return PracticeResult(mix: mix, trace: trace)
    }

    private mutating func updateMatch(at time: TimeInterval) {
        guard let tempoDifference else { return }
        defer { lastTempoSample = time }
        guard abs(tempoDifference) < PracticeSession.matchToleranceBPM else {
            candidateStart = nil
            pending = MatchedStats()
            return
        }
        guard let candidateStart, let lastTempoSample,
              time - lastTempoSample <= PracticeSession.maximumSampleInterval
        else {
            candidateStart = time
            pending = MatchedStats()
            return
        }
        if time - candidateStart >= PracticeSession.matchHoldSeconds {
            matchedAt = max(start, candidateStart - PracticeDeckTempo.window / 2)
            matched = pending
            pending = MatchedStats()
        }
    }

    private mutating func appendTrace(at time: TimeInterval) {
        guard let phaseError else { return }
        let seconds = time - start
        if let last = trace.last, seconds - last.seconds < traceInterval - 0.004 { return }
        trace.append(PracticeTraceSample(seconds: Self.rounded(seconds, places: 3),
                                         errorMilliseconds: Self.rounded(phaseError, places: 2)))
        if trace.count >= PracticeSession.traceCapacity {
            trace = stride(from: 0, to: trace.count, by: 2).map { trace[$0] }
            traceInterval *= 2
        }
    }

    private static func phaseError(outgoing: LiveDeckSnapshot, incoming: LiveDeckSnapshot, slaveBPM: Double) -> Double? {
        guard let outgoingPhase = outgoing.phaseInBar, let incomingPhase = incoming.phaseInBar, slaveBPM > 3 else { return nil }
        let beats = incomingPhase - outgoingPhase
        let wrapped = beats - (beats + 0.5).rounded(.down)
        return wrapped * 60_000 / slaveBPM
    }

    private static func rounded(_ value: Double, places: Int) -> Double {
        let scale = pow(10, Double(places))
        return (value * scale).rounded() / scale
    }
}

private struct MatchedStats: Sendable {
    private var errorSum = 0.0
    private var errorCount = 0
    private(set) var worstError: Double?
    private var tempoSum = 0.0
    private var tempoCount = 0

    mutating func add(error: Double?, tempoDifference: Double?) {
        if let error {
            errorSum += abs(error)
            errorCount += 1
            worstError = max(worstError ?? 0, abs(error))
        }
        if let tempoDifference {
            tempoSum += tempoDifference
            tempoCount += 1
        }
    }

    var meanError: Double? { errorCount > 0 ? errorSum / Double(errorCount) : nil }
    var meanTempoDifference: Double? { tempoCount > 0 ? tempoSum / Double(tempoCount) : nil }
}
