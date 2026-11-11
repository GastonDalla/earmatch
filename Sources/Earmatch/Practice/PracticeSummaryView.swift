import EarmatchCore
import SwiftUI

struct PracticeSummaryView: View {
    let mix: PracticeMix

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.small) {
            ViewThatFits(in: .horizontal) {
                HStack(alignment: .firstTextBaseline, spacing: Spacing.small) {
                    PracticeLabel(text: PracticeStrings.lastMix)
                    Spacer(minLength: Spacing.small)
                    detail.fixedSize()
                }
                VStack(alignment: .leading, spacing: Spacing.tight) {
                    PracticeLabel(text: PracticeStrings.lastMix)
                    detail.minimumScaleFactor(0.85)
                }
            }
            Grid(horizontalSpacing: Spacing.small, verticalSpacing: Spacing.small) {
                GridRow {
                    PracticeMetric(title: PracticeStrings.tempoMatch,
                                   value: mix.secondsToMatch.map(PracticeStrings.duration) ?? PracticeStrings.notMatched,
                                   colour: mix.secondsToMatch == nil ? WindowTheme.waiting : WindowTheme.primaryText)
                    PracticeMetric(title: PracticeStrings.drift,
                                   value: mix.tempoDriftBPM.map(PracticeStrings.bpm) ?? PracticeStrings.noValue)
                }
                GridRow {
                    PracticeMetric(title: PracticeStrings.averageError,
                                   value: mix.meanPhaseErrorMilliseconds.map { PracticeStrings.milliseconds($0) } ?? PracticeStrings.noValue,
                                   colour: errorColour(mix.meanPhaseErrorMilliseconds))
                    PracticeMetric(title: PracticeStrings.worstError,
                                   value: mix.worstPhaseErrorMilliseconds.map { PracticeStrings.milliseconds($0) } ?? PracticeStrings.noValue,
                                   colour: errorColour(mix.worstPhaseErrorMilliseconds))
                }
            }
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(PracticeStrings.summaryAccessibility(mix))
    }

    private var detail: some View {
        Text(PracticeStrings.lastMixDetail(mix))
            .font(TypeScale.status)
            .monospacedDigit()
            .foregroundStyle(WindowTheme.secondaryText)
            .lineLimit(1)
    }

    private func errorColour(_ milliseconds: Double?) -> Color {
        guard let milliseconds else { return WindowTheme.primaryText }
        return milliseconds <= PracticeErrorMeter.inPhase ? WindowTheme.accent : WindowTheme.primaryText
    }
}

struct PracticeLabel: View {
    let text: String

    var body: some View {
        Text(text)
            .font(TypeScale.label)
            .tracking(2.2)
            .foregroundStyle(WindowTheme.sectionText)
            .accessibilityAddTraits(.isHeader)
    }
}

private struct PracticeMetric: View {
    let title: String
    let value: String
    var colour: Color = WindowTheme.primaryText

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(value)
                .font(.system(size: 17, weight: .semibold))
                .monospacedDigit()
                .foregroundStyle(colour)
                .lineLimit(1)
                .minimumScaleFactor(0.8)
            Text(title)
                .font(TypeScale.caption)
                .foregroundStyle(WindowTheme.secondaryText)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .surface(cornerRadius: 10, fill: WindowTheme.well, stroke: WindowTheme.hairline)
    }
}
