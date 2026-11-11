import EarmatchCore
import SwiftUI

struct PracticeLiveSection: View {
    let practice: PracticeController
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        ZStack(alignment: .topLeading) {
            if let live = practice.live {
                PracticeLiveMix(live: live).transition(.opacity)
            } else {
                PracticeWaiting().transition(.opacity)
            }
        }
        .animation(WindowTheme.motion(reduceMotion, duration: 0.25), value: practice.isMixing)
    }
}

private struct PracticeLiveMix: View {
    let live: PracticeLiveStatus

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.small) {
            HStack(alignment: .firstTextBaseline, spacing: Spacing.small) {
                Text(PracticeStrings.deckPair(live.outgoingDeck, live.incomingDeck))
                    .font(TypeScale.bodyStrong)
                    .foregroundStyle(WindowTheme.primaryText)
                Spacer(minLength: Spacing.small)
                Text(PracticeStrings.duration(live.elapsedSeconds))
                    .font(TypeScale.status)
                    .monospacedDigit()
                    .foregroundStyle(WindowTheme.secondaryText)
            }
            TempoMatchLine(live: live)
            HStack(spacing: Spacing.medium) {
                PracticeErrorMeter(errorMilliseconds: live.phaseErrorMilliseconds, isDimmed: !live.isMatched)
                    .frame(height: 34)
                Text(live.phaseErrorMilliseconds.map { PracticeStrings.milliseconds($0, signed: true) } ?? PracticeStrings.noValue)
                    .font(.system(size: 17, weight: .semibold, design: .monospaced))
                    .foregroundStyle(errorColour)
                    .frame(width: 76, alignment: .trailing)
            }
            Text(caption)
                .font(TypeScale.caption)
                .foregroundStyle(WindowTheme.secondaryText)
                .lineLimit(1)
                .minimumScaleFactor(0.9)
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(PracticeStrings.liveAccessibility(live))
        .accessibilityAddTraits(.updatesFrequently)
    }

    private var errorColour: Color {
        guard let error = live.phaseErrorMilliseconds, live.isMatched else { return WindowTheme.secondaryText }
        return PracticeErrorMeter.color(for: error)
    }

    private var caption: String {
        if !live.isLongEnough { return PracticeStrings.waitingForTempo }
        if live.phaseErrorMilliseconds == nil && live.isMatched { return PracticeStrings.noPhase }
        return PracticeStrings.averageSinceMatch(live.meanPhaseErrorMilliseconds)
    }
}

private struct TempoMatchLine: View {
    let live: PracticeLiveStatus

    var body: some View {
        let colour = live.isMatched ? WindowTheme.accent : WindowTheme.waiting
        HStack(spacing: Spacing.small) {
            Image(systemName: live.isMatched ? "checkmark.circle.fill" : "metronome")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(colour)
            Text(live.secondsToMatch.map(PracticeStrings.matchedAfter) ?? PracticeStrings.matching(live.tempoDifferenceBPM))
                .font(TypeScale.status)
                .monospacedDigit()
                .foregroundStyle(WindowTheme.primaryText.opacity(0.88))
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 4)
        .surface(cornerRadius: 8, fill: colour.opacity(0.10), stroke: colour.opacity(0.22))
    }
}

private struct PracticeWaiting: View {
    var body: some View {
        HStack(alignment: .top, spacing: Spacing.medium) {
            Image(systemName: "metronome")
                .font(.system(size: 18, weight: .medium))
                .foregroundStyle(WindowTheme.sectionText)
                .frame(width: 30, height: 30)
                .background(WindowTheme.well, in: RoundedRectangle(cornerRadius: 8, style: .continuous))
                .accessibilityHidden(true)
            VStack(alignment: .leading, spacing: 2) {
                Text(PracticeStrings.waitingTitle)
                    .font(TypeScale.bodyStrong)
                    .foregroundStyle(WindowTheme.primaryText)
                Text(PracticeStrings.waitingBody)
                    .font(TypeScale.caption)
                    .foregroundStyle(WindowTheme.secondaryText)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: 0)
        }
        .accessibilityElement(children: .combine)
    }
}
