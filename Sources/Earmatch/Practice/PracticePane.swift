import EarmatchCore
import SwiftUI

struct PracticePane: View {
    let model: MainWindowModel
    let practice: PracticeController
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        let overview = practice.overview
        PaneScaffold(title: MainWindowPane.practice.title) {
            PaneColumns {
                PaneCard {
                    OptionSwitchRow(title: OptionText.mixTrainerTitle,
                                    subtitle: OptionText.mixTrainerSubtitle,
                                    isOn: model.binding(\.isMixTrainerEnabled))
                }
                PracticeSessionCard(practice: practice)
            } trailing: {
                if let mix = overview.lastMix {
                    if let trace = overview.lastTrace, trace.mixID == mix.id, !trace.samples.isEmpty {
                        PaneCard { PracticeReplayView(trace: trace, traceHeight: 76) }
                    }
                    if !overview.days.isEmpty {
                        PaneCard { PracticeProgressChart(days: overview.days, chartHeight: 76) }
                    }
                } else {
                    PracticeEmptyHistory()
                }
            }
            .opacity(model.settings.isEnabled ? 1 : 0.55)
            .animation(WindowTheme.motion(reduceMotion, duration: 0.25), value: overview.lastMix?.id)
            .animation(WindowTheme.motion(reduceMotion), value: model.settings.isEnabled)
        }
    }
}

private struct PracticeSessionCard: View {
    let practice: PracticeController

    var body: some View {
        PaneCard {
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
            if let mix = practice.overview.lastMix {
                Rectangle().fill(WindowTheme.hairline).frame(height: 1)
                PracticeSummaryView(mix: mix)
                    .transition(.opacity)
            }
            PracticeStatusRow(practice: practice)
        }
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
                .font(.system(size: 26, weight: .medium))
                .foregroundStyle(WindowTheme.sectionText)
                .accessibilityHidden(true)
            Text(PracticeStrings.emptyTitle)
                .font(TypeScale.headline)
                .foregroundStyle(WindowTheme.primaryText)
            Text(PracticeStrings.emptyBody)
                .font(TypeScale.caption)
                .foregroundStyle(WindowTheme.secondaryText)
                .multilineTextAlignment(.center)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: 280)
        }
        .padding(Spacing.large)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .surface(cornerRadius: 14, fill: WindowTheme.card, stroke: WindowTheme.hairline)
        .accessibilityElement(children: .combine)
    }
}
