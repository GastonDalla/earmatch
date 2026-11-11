import EarmatchCore
import Foundation

enum PracticeStrings {
    static var title: String { L10n.text("Last mixes") }
    static var subtitle: String { L10n.text("Times every mix you make by ear") }
    static var waitingTitle: String { L10n.text("Waiting for a mix") }
    static var waitingBody: String { L10n.text("Play both decks together. Each mix of 8 seconds or more is timed and saved.") }
    static var emptyTitle: String { L10n.text("No mixes yet") }
    static var emptyBody: String { L10n.text("Your mixes show up here with a replay of the last one and your progress day by day.") }
    static var lastMix: String { L10n.text("LAST MIX") }
    static var replay: String { L10n.text("REPLAY") }
    static var progress: String { L10n.text("PROGRESS") }
    static var tempoMatch: String { L10n.text("Tempo match") }
    static var drift: String { L10n.text("Drift") }
    static var averageError: String { L10n.text("Avg error") }
    static var worstError: String { L10n.text("Worst") }
    static var notMatched: String { L10n.text("Not matched") }
    static let noValue = "—"
    static var behind: String { L10n.text("BEHIND") }
    static var ahead: String { L10n.text("AHEAD") }
    static var play: String { L10n.text("Replay") }
    static var stop: String { L10n.text("Stop") }
    static var playHint: String { L10n.text("Replays the phase error of the last transition") }
    static var stopHint: String { L10n.text("Stops the replay") }
    static var medianPerDay: String { L10n.text("Median phase error per day") }
    static var today: String { L10n.text("Today") }
    static var waitingForTempo: String { L10n.text("Saved once the decks play 8 seconds together") }
    static var noPhase: String { L10n.text("Reading the beat grids…") }
    static var matchingTempos: String { L10n.text("Matching tempos") }
    static var averageAfterMatch: String { L10n.text("Average appears once the tempos match") }
    static var waitingForDecks: String { L10n.text("Waiting for both decks to play") }
    static var temposNeverMatched: String { L10n.text("tempos never matched") }
    static var inPhase: String { L10n.text("in phase") }
    static var timeAxis: String { L10n.text("Time") }
    static var phaseErrorAxis: String { L10n.text("Phase error") }

    private static var deckPairFormat: String { L10n.template("Deck %ld → Deck %ld") }
    private static var matchedAfterFormat: String { L10n.template("Tempos matched in %@") }
    private static var matchingFormat: String { L10n.template("Matching tempos · %@") }
    private static var averageSinceMatchFormat: String { L10n.template("Average %@ since the tempos matched") }
    private static var worstAtFormat: String { L10n.template("Worst at %@") }
    private static var downFormat: String { L10n.template("Down from %@ to %@") }
    private static var upFormat: String { L10n.template("Up from %@ to %@") }
    private static var steadyFormat: String { L10n.template("Steady at %@") }
    private static var timingFormat: String { L10n.template("Timing deck %ld → deck %ld") }
    private static var mixingIntoFormat: String { L10n.template("Mixing deck %ld into deck %ld for %@") }
    private static var temposMatchedAfterFormat: String { L10n.template("tempos matched after %@") }
    private static var temposApartFormat: String { L10n.template("tempos %@ apart") }
    private static var phaseErrorFormat: String { L10n.template("phase error %@") }
    private static var lastMixFormat: String { L10n.template("Last mix, %@, %@ long") }
    private static var driftFormat: String { L10n.template("drift %@") }
    private static var averagePhaseErrorFormat: String { L10n.template("average phase error %@") }
    private static var worstFormat: String { L10n.template("worst %@") }
    private static var replaySummaryFormat: String { L10n.template("Phase error of the last transition, %@ long") }
    private static var temposMatchedAtFormat: String { L10n.template("tempos matched at %@") }
    private static var worstErrorAtFormat: String { L10n.template("worst %@ at %@") }
    private static var noPhaseErrorFormat: String { L10n.template("%@, no phase error") }
    private static var aheadFormat: String { L10n.template("%@ ahead") }
    private static var behindFormat: String { L10n.template("%@ behind") }

    private static var locale: Locale { AppLanguage.locale }

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
