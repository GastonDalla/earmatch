import EarmatchCore
import Foundation

enum PracticeStrings {
    static let sectionTitle = L10n.text("PRACTICE")
    static let title = L10n.text("Last mixes")
    static let subtitle = L10n.text("Times every mix you make by ear")
    static let waitingTitle = L10n.text("Waiting for a mix")
    static let waitingBody = L10n.text("Play both decks together. Each mix of 8 seconds or more is timed and saved.")
    static let emptyTitle = L10n.text("No mixes yet")
    static let emptyBody = L10n.text("Your mixes show up here with a replay of the last one and your progress day by day.")
    static let lastMix = L10n.text("LAST MIX")
    static let replay = L10n.text("REPLAY")
    static let progress = L10n.text("PROGRESS")
    static let tempoMatch = L10n.text("Tempo match")
    static let drift = L10n.text("Drift")
    static let averageError = L10n.text("Avg error")
    static let worstError = L10n.text("Worst")
    static let notMatched = L10n.text("Not matched")
    static let noValue = "—"
    static let behind = L10n.text("BEHIND")
    static let ahead = L10n.text("AHEAD")
    static let play = L10n.text("Replay")
    static let stop = L10n.text("Stop")
    static let playHint = L10n.text("Replays the phase error of the last transition")
    static let stopHint = L10n.text("Stops the replay")
    static let medianPerDay = L10n.text("Median phase error per day")
    static let today = L10n.text("Today")
    static let waitingForTempo = L10n.text("Saved once the decks play 8 seconds together")
    static let noPhase = L10n.text("Reading the beat grids…")
    static let matchingTempos = L10n.text("Matching tempos")
    static let averageAfterMatch = L10n.text("Average appears once the tempos match")
    static let waitingForDecks = L10n.text("Waiting for both decks to play")
    static let temposNeverMatched = L10n.text("tempos never matched")
    static let inPhase = L10n.text("in phase")
    static let timeAxis = L10n.text("Time")
    static let phaseErrorAxis = L10n.text("Phase error")

    private static let deckPairFormat = L10n.template("Deck %ld → Deck %ld")
    private static let matchedAfterFormat = L10n.template("Tempos matched in %@")
    private static let matchingFormat = L10n.template("Matching tempos · %@")
    private static let averageSinceMatchFormat = L10n.template("Average %@ since the tempos matched")
    private static let worstAtFormat = L10n.template("Worst at %@")
    private static let downFormat = L10n.template("Down from %@ to %@")
    private static let upFormat = L10n.template("Up from %@ to %@")
    private static let steadyFormat = L10n.template("Steady at %@")
    private static let timingFormat = L10n.template("Timing deck %ld → deck %ld")
    private static let mixingIntoFormat = L10n.template("Mixing deck %ld into deck %ld for %@")
    private static let temposMatchedAfterFormat = L10n.template("tempos matched after %@")
    private static let temposApartFormat = L10n.template("tempos %@ apart")
    private static let phaseErrorFormat = L10n.template("phase error %@")
    private static let lastMixFormat = L10n.template("Last mix, %@, %@ long")
    private static let driftFormat = L10n.template("drift %@")
    private static let averagePhaseErrorFormat = L10n.template("average phase error %@")
    private static let worstFormat = L10n.template("worst %@")
    private static let replaySummaryFormat = L10n.template("Phase error of the last transition, %@ long")
    private static let temposMatchedAtFormat = L10n.template("tempos matched at %@")
    private static let worstErrorAtFormat = L10n.template("worst %@ at %@")
    private static let noPhaseErrorFormat = L10n.template("%@, no phase error")
    private static let aheadFormat = L10n.template("%@ ahead")
    private static let behindFormat = L10n.template("%@ behind")

    private static let locale = AppLanguage.locale

    private static func fill(_ format: String, _ arguments: any CVarArg...) -> String {
        String(format: format, locale: locale, arguments: arguments)
    }

    static func deckPair(_ outgoing: Int, _ incoming: Int) -> String {
        fill(deckPairFormat, outgoing, incoming)
    }

    static func matchedAfter(_ seconds: Double) -> String {
        fill(matchedAfterFormat, duration(seconds))
    }

    static func matching(_ difference: Double?) -> String {
        guard let difference else { return matchingTempos }
        return fill(matchingFormat, bpm(difference))
    }

    static func averageSinceMatch(_ milliseconds: Double?) -> String {
        guard let milliseconds else { return averageAfterMatch }
        return fill(averageSinceMatchFormat, self.milliseconds(milliseconds))
    }

    static func lastMixDetail(_ mix: PracticeMix) -> String {
        "\(deckPair(mix.outgoingDeck, mix.incomingDeck)) · \(endTime(mix.endedAt)) · \(duration(mix.bothPlayingSeconds))"
    }

    static func worstAt(_ seconds: Double) -> String {
        fill(worstAtFormat, duration(seconds))
    }

    static func replayPosition(_ seconds: Double, of total: Double) -> String {
        "\(duration(seconds)) / \(duration(total))"
    }

    static func trend(from first: Double, to last: Double) -> String {
        let change = last.rounded() - first.rounded()
        if change < 0 { return fill(downFormat, milliseconds(first), milliseconds(last)) }
        if change > 0 { return fill(upFormat, milliseconds(first), milliseconds(last)) }
        return fill(steadyFormat, milliseconds(last))
    }

    static func statusMixing(_ outgoing: Int, _ incoming: Int) -> String {
        fill(timingFormat, outgoing, incoming)
    }

    static func statusWaiting(savedMixes: Int) -> String {
        savedMixes == 0 ? waitingForDecks : L10n.plural("Waiting for both decks · %ld mixes saved", savedMixes)
    }

    static func milliseconds(_ value: Double, signed: Bool = false) -> String {
        let rounded = value.rounded()
        let number = rounded == 0 ? 0 : rounded
        let style = FloatingPointFormatStyle<Double>.number.precision(.fractionLength(0)).locale(locale)
        let text = signed ? number.formatted(style.sign(strategy: .always(includingZero: false))) : number.formatted(style)
        return "\(text) ms"
    }

    static func bpm(_ value: Double) -> String {
        let rounded = (value * 100).rounded() / 100
        let number = rounded == 0 ? 0 : rounded
        let style = FloatingPointFormatStyle<Double>.number.precision(.fractionLength(2))
            .sign(strategy: .always(includingZero: false)).locale(locale)
        return "\(number.formatted(style)) BPM"
    }

    static func duration(_ seconds: Double) -> String {
        Duration.seconds(max(0, seconds.rounded())).formatted(.time(pattern: .minuteSecond).locale(locale))
    }

    static func shortDay(_ date: Date, calendar: Calendar = .autoupdatingCurrent) -> String {
        calendar.isDateInToday(date) ? today : date.formatted(.dateTime.month(.abbreviated).day().locale(locale))
    }

    static func endTime(_ date: Date, calendar: Calendar = .autoupdatingCurrent) -> String {
        calendar.isDateInToday(date)
            ? date.formatted(Date.FormatStyle(date: .omitted, time: .shortened, locale: locale))
            : date.formatted(.dateTime.month(.abbreviated).day().hour().minute().locale(locale))
    }

    static func liveAccessibility(_ live: PracticeLiveStatus) -> String {
        var parts = [fill(mixingIntoFormat, live.outgoingDeck, live.incomingDeck, spokenDuration(live.elapsedSeconds))]
        if let matched = live.secondsToMatch {
            parts.append(fill(temposMatchedAfterFormat, spokenDuration(matched)))
        } else if let difference = live.tempoDifferenceBPM {
            parts.append(fill(temposApartFormat, bpm(abs(difference))))
        }
        if let error = live.phaseErrorMilliseconds { parts.append(fill(phaseErrorFormat, spokenError(error))) }
        return parts.joined(separator: ". ")
    }

    static func summaryAccessibility(_ mix: PracticeMix) -> String {
        var parts = [fill(lastMixFormat, deckPair(mix.outgoingDeck, mix.incomingDeck), spokenDuration(mix.bothPlayingSeconds))]
        if let match = mix.secondsToMatch {
            parts.append(fill(temposMatchedAfterFormat, spokenDuration(match)))
        } else {
            parts.append(temposNeverMatched)
        }
        if let drift = mix.tempoDriftBPM { parts.append(fill(driftFormat, bpm(drift))) }
        if let average = mix.meanPhaseErrorMilliseconds { parts.append(fill(averagePhaseErrorFormat, milliseconds(average))) }
        if let worst = mix.worstPhaseErrorMilliseconds { parts.append(fill(worstFormat, milliseconds(worst))) }
        return parts.joined(separator: ", ")
    }

    static func replayAccessibility(duration total: Double, matchedAt: Double?, worst: (seconds: Double, error: Double)?) -> String {
        var parts = [fill(replaySummaryFormat, spokenDuration(total))]
        if let matchedAt { parts.append(fill(temposMatchedAtFormat, duration(matchedAt))) }
        if let worst { parts.append(fill(worstErrorAtFormat, spokenError(worst.error), duration(worst.seconds))) }
        return parts.joined(separator: ", ")
    }

    static func replayValue(seconds: Double, error: Double?) -> String {
        guard let error else { return duration(seconds) }
        return "\(duration(seconds)), \(spokenError(error))"
    }

    static func progressAccessibility(_ days: [(day: String, error: Double?)]) -> String {
        let entries = days.map { day in day.error.map { "\(day.day), \(milliseconds($0))" } ?? fill(noPhaseErrorFormat, day.day) }
        return "\(medianPerDay): \(entries.joined(separator: "; "))"
    }

    static func spokenError(_ milliseconds: Double) -> String {
        let magnitude = self.milliseconds(abs(milliseconds))
        if milliseconds.rounded() == 0 { return inPhase }
        return fill(milliseconds > 0 ? aheadFormat : behindFormat, magnitude)
    }

    static func spokenDuration(_ seconds: Double) -> String {
        Duration.seconds(max(0, seconds.rounded())).formatted(.units(allowed: [.minutes, .seconds], width: .wide).locale(locale))
    }
}
