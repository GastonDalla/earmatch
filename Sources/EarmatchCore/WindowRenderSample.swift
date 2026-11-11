import Foundation

public enum WindowRenderSample {
    public static let rekordboxVersion = "7.2.4"
    public static let trackTitles = ["Night Shift Signal", "Glasshouse Dub", "Concrete Bloom", "Tidewater Pulse"]

    private static let bpm = 128.0
    private static let masterDeck = 2
    private static let phases: [Int: Double] = [1: 2.46, 2: 2.5, 3: 1.93, 4: 2.53]
    private static let bars: [Int: Int] = [1: 17, 2: 49, 3: 33, 4: 9]

    public static var settings: OverlaySettings {
        var settings = OverlaySettings()
        BoothPreset.cdj3000.apply(to: &settings)
        return settings
    }

    public static func decks(count: Int) -> LiveDecksSnapshot {
        let numbers = Array(1 ... max(1, min(count, trackTitles.count)))
        let inputs = numbers.map { number in
            let phase = phases[number] ?? 0
            return PhaseMeterDeckInput(number: number, phaseInBar: phase,
                                       counter: BarPosition(bar: bars[number] ?? 1, beat: Int(phase) + 1))
        }
        let style = settings.phaseMeterType
        let decks = numbers.map { number in
            LiveDeckSnapshot(number: number, title: trackTitles[number - 1], isMaster: number == masterDeck,
                             meter: PhaseMeterModel.display(for: number, decks: inputs, master: masterDeck, style: style),
                             phaseInBar: phases[number], beatsPerSecond: bpm / 60)
        }
        return LiveDecksSnapshot(decks: decks, framesPerSecond: 60, latencyMilliseconds: 7.4)
    }

    public static func statusContext(deckCount: Int) -> StatusContext {
        let layout: DeckArrangement = deckCount > 2 ? .fourDeckHorizontal : .twoDeckHorizontal
        return StatusContext(activation: .active, settings: settings, isScreenRecordingGranted: true,
                             rekordboxVersion: rekordboxVersion, captureProblem: nil, layoutName: layout.rawValue)
    }

    public static let practiceLive = PracticeLiveStatus(
        outgoingDeck: 2, incomingDeck: 1, elapsedSeconds: 46, bothPlayingSeconds: 46,
        tempoDifferenceBPM: 0.01, secondsToMatch: 11, phaseErrorMilliseconds: 6, meanPhaseErrorMilliseconds: 9
    )

    public static func practiceOverview(now: Date, calendar: Calendar) -> PracticeOverview {
        let dailyErrors: [(daysAgo: Int, errors: [Double])] = [
            (11, [41, 37]), (10, [36]), (8, [33, 29, 31]), (7, [27]), (5, [24, 21]),
            (4, [19, 22]), (2, [16, 14, 17]), (1, [13]), (0, [12, 9, 11]),
        ]
        let today = calendar.startOfDay(for: now)
        var mixes: [PracticeMix] = []
        for (dayIndex, entry) in dailyErrors.enumerated() {
            guard let day = calendar.date(byAdding: .day, value: -entry.daysAgo, to: today) else { continue }
            for (mixIndex, error) in entry.errors.enumerated() {
                let ended = if entry.daysAgo == 0 {
                    min(now, max(today.addingTimeInterval(Double(mixIndex + 1)),
                                 now.addingTimeInterval(-240 - Double(entry.errors.count - 1 - mixIndex) * 300)))
                } else {
                    day.addingTimeInterval(19 * 3600 + Double(mixIndex) * 900)
                }
                let length = 52 + Double((dayIndex + mixIndex) % 4) * 9
                mixes.append(PracticeMix(
                    id: sampleID(dayIndex * 10 + mixIndex), startedAt: ended.addingTimeInterval(-length), endedAt: ended,
                    outgoingDeck: mixIndex.isMultiple(of: 2) ? 2 : 1, incomingDeck: mixIndex.isMultiple(of: 2) ? 1 : 2,
                    bothPlayingSeconds: length, secondsToMatch: 26 - Double(dayIndex) * 1.6,
                    tempoDriftBPM: 0.04 - Double(dayIndex) * 0.003,
                    meanPhaseErrorMilliseconds: error, worstPhaseErrorMilliseconds: error * 2.3
                ))
            }
        }
        let last = mixes[mixes.count - 1]
        let trace = trace(for: last)
        return PracticeOverview(history: PracticeHistory(mixes: mixes, lastTrace: trace), calendar: calendar)
    }

    private static func trace(for mix: PracticeMix) -> PracticeTrace {
        let duration = mix.bothPlayingSeconds
        let matched = mix.secondsToMatch ?? 0
        let samples = stride(from: 0.0, through: duration, by: 0.25).map { seconds -> PracticeTraceSample in
            let error = if seconds < matched {
                62 * cos(seconds * 0.9) * (1 - seconds / matched * 0.6) + 18 * sin(seconds * 3.1)
            } else {
                8 * sin(seconds * 0.7) + 5 * sin(seconds * 2.3 + 1) - 2
            }
            return PracticeTraceSample(seconds: seconds, errorMilliseconds: (error * 10).rounded() / 10)
        }
        return PracticeTrace(mixID: mix.id, durationSeconds: duration, matchedAtSeconds: matched, samples: samples)
    }

    private static func sampleID(_ index: Int) -> UUID {
        UUID(uuidString: String(format: "00000000-0000-4000-8000-%012d", index)) ?? UUID()
    }
}
