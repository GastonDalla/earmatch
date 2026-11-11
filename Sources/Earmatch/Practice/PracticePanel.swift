import EarmatchCore
import SwiftUI

struct PracticePanel: View {
    let practice: PracticeController
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        let overview = practice.overview
        OptionCard {
            VStack(alignment: .leading, spacing: 2) {
                Text(PracticeStrings.title)
                    .font(TypeScale.headline)
                    .foregroundStyle(WindowTheme.primaryText)
                    .accessibilityAddTraits(.isHeader)
                Text(PracticeStrings.subtitle)
                    .font(TypeScale.caption)
                    .foregroundStyle(WindowTheme.secondaryText)
            }
            PracticeLiveSection(practice: practice)
            Rectangle().fill(WindowTheme.hairline).frame(height: 1)
            if let mix = overview.lastMix {
                VStack(alignment: .leading, spacing: Spacing.large) {
                    PracticeSummaryView(mix: mix)
                    if let trace = overview.lastTrace, trace.mixID == mix.id, !trace.samples.isEmpty {
                        PracticeReplayView(trace: trace)
                    }
                    if !overview.days.isEmpty {
                        PracticeProgressChart(days: overview.days)
                    }
                }
                .transition(.opacity)
            } else {
                PracticeEmptyHistory().transition(.opacity)
            }
            PracticeStatusRow(practice: practice)
        }
        .animation(WindowTheme.motion(reduceMotion, duration: 0.25), value: overview.lastMix?.id)
        .task { await practice.loadHistory() }
    }
}

private struct PracticeStatusRow: View {
    let practice: PracticeController

    var body: some View {
        let line = if let live = practice.live, practice.isMixing {
            StatusLine(tone: .live, text: PracticeStrings.statusMixing(live.outgoingDeck, live.incomingDeck))
        } else {
            StatusLine(tone: .waiting, text: PracticeStrings.statusWaiting(savedMixes: practice.overview.mixCount))
        }
        CardStatusRow(line: line)
    }
}

private struct PracticeEmptyHistory: View {
    var body: some View {
        VStack(spacing: Spacing.small) {
            Image(systemName: "chart.line.uptrend.xyaxis")
                .font(.system(size: 22, weight: .medium))
                .foregroundStyle(WindowTheme.sectionText)
                .accessibilityHidden(true)
            Text(PracticeStrings.emptyTitle)
                .font(TypeScale.bodyStrong)
                .foregroundStyle(WindowTheme.primaryText)
            Text(PracticeStrings.emptyBody)
                .font(TypeScale.caption)
                .foregroundStyle(WindowTheme.secondaryText)
                .multilineTextAlignment(.center)
                .fixedSize(horizontal: false, vertical: true)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, Spacing.medium)
        .padding(.horizontal, Spacing.medium)
        .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(WindowTheme.well))
        .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).strokeBorder(WindowTheme.hairline))
        .accessibilityElement(children: .combine)
    }
}
