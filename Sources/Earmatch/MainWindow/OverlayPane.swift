import SwiftUI
import EarmatchCore

struct OverlayPane: View {
    let model: MainWindowModel
    let live: LiveDecksModel
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        PaneScaffold(title: MainWindowPane.overlay.title) {
            PaneColumns {
                BPMPrecisionCard(model: model)
                WaveformCard(model: model, live: live)
            } trailing: {
                PhaseMeterCard(model: model, live: live)
            }
            .opacity(model.settings.isEnabled ? 1 : 0.55)
            .animation(WindowTheme.motion(reduceMotion), value: model.settings.isEnabled)
        }
    }
}

private struct BPMPrecisionCard: View {
    let model: MainWindowModel

    var body: some View {
        let settings = model.settings
        PaneCard(title: WindowText.bpmPrecisionSection) {
            OptionSwitchRow(title: WindowText.limitBPM,
                            subtitle: WindowText.limitBPMSubtitle,
                            isOn: model.binding(\.isBPMLimitEnabled))
            VStack(alignment: .leading, spacing: 6) {
                OptionPicker(title: WindowText.bpmPrecision, options: BPMLimit.allCases.map { ($0.displayName, $0) },
                             explanation: \.explanation, selection: model.binding(\.bpmLimit))
                ExplanationLine(text: settings.bpmLimit.explanation)
            }
            .opacity(settings.isBPMLimitEnabled ? 1 : 0.45)
            OptionSwitchRow(title: OptionText.blindModeTitle,
                            subtitle: OptionText.blindModeSubtitle,
                            isOn: model.binding(\.isBlindModeEnabled))
            CardStatusRow(line: StatusText.bpmPrecision(model.status)) {
                BPMSampleView(limit: settings.isBPMLimitEnabled ? settings.bpmLimit : nil, isPaused: !model.isVisible)
            }
        }
    }
}

private struct BPMSampleView: View {
    let limit: BPMLimit?
    let isPaused: Bool

    var body: some View {
        TimelineView(.animation(minimumInterval: 0.25, paused: isPaused)) { timeline in
            let seconds = timeline.date.timeIntervalSinceReferenceDate
            let sample = BPMSample(bpm: 128 + 0.06 * sin(seconds * 0.9), limit: limit)
            HStack(spacing: 1) {
                Text(sample.visible).foregroundStyle(WindowTheme.primaryText)
                if !sample.covered.isEmpty {
                    Text(sample.covered)
                        .foregroundStyle(.clear)
                        .padding(.horizontal, 1)
                        .surface(RoundedRectangle(cornerRadius: 3), fill: Color.black, stroke: WindowTheme.accent, lineWidth: 1.5)
                }
            }
            .font(.system(size: 15, weight: .semibold, design: .monospaced))
            .fixedSize()
            .accessibilityElement(children: .ignore)
            .accessibilityLabel(sample.visible.isEmpty ? WindowText.sampleHidden : WindowText.sample(reading: sample.visible))
        }
    }
}

private struct WaveformCard: View {
    let model: MainWindowModel
    let live: LiveDecksModel
    @State private var isPeeking = false

    var body: some View {
        let mode = model.settings.waveformMode
        PaneCard(title: WindowText.waveformSection) {
            LivePeekControl(model: model, live: live, source: .waveform, isPeeking: $isPeeking)
        } content: {
            VStack(alignment: .leading, spacing: 6) {
                OptionPicker(title: WindowText.waveform, options: WaveformMode.allCases.map { ($0.displayName, $0) },
                             explanation: \.explanation, selection: model.binding(\.waveformMode))
                ExplanationLine(text: mode.explanation)
            }
            PreviewScreen(height: 54) {
                LiveWaveformPreview(model: model, live: live, mode: isPeeking ? .normal : mode)
            }
            CardStatusRow(line: StatusText.waveform(model.status))
        }
    }
}

private struct LiveWaveformPreview: View {
    let model: MainWindowModel
    let live: LiveDecksModel
    let mode: WaveformMode

    var body: some View {
        WaveformPreview(mode: mode, live: LivePreviewSource.phases(model: model, live: live), isPaused: !model.isVisible)
    }
}

private struct PhaseMeterCard: View {
    let model: MainWindowModel
    let live: LiveDecksModel
    @State private var isPeeking = false

    var body: some View {
        let settings = model.settings
        PaneCard(title: WindowText.phaseMeterSection) {
            LivePeekControl(model: model, live: live, source: .phaseMeter, isPeeking: $isPeeking)
        } content: {
            OptionSwitchRow(title: WindowText.phaseMeter,
                            subtitle: WindowText.phaseMeterSubtitle,
                            isOn: model.binding(\.isPhaseMeterEnabled))
            VStack(alignment: .leading, spacing: 6) {
                OptionPicker(title: WindowText.phaseMeterType, options: PhaseMeterType.allCases.map { ($0.displayName, $0) },
                             explanation: \.explanation, selection: model.binding(\.phaseMeterType))
                ExplanationLine(text: settings.phaseMeterType.explanation)
            }
            .opacity(settings.isPhaseMeterEnabled ? 1 : 0.45)
            VStack(alignment: .leading, spacing: 6) {
                Text(OptionText.counterTitle).font(TypeScale.bodyStrong).foregroundStyle(WindowTheme.secondaryText)
                    .padding(.leading, Spacing.tight)
                    .accessibilityHidden(true)
                OptionPicker(title: OptionText.counterTitle, options: CounterStyle.allCases.map { ($0.displayName, $0) },
                             explanation: { WindowText.counterHelp($0) }, selection: model.binding(\.counterStyle))
                ExplanationLine(text: settings.counterStyle.explanation)
            }
            .opacity(settings.isPhaseMeterEnabled ? 1 : 0.45)
            PreviewScreen(height: 64) {
                if !isPeeking {
                    LivePhaseMeterPreview(model: model, live: live)
                        .opacity(settings.isPhaseMeterEnabled ? 1 : 0.3)
                }
            }
            CardStatusRow(line: StatusText.phaseMeter(model.status))
        }
    }
}

private struct LivePhaseMeterPreview: View {
    let model: MainWindowModel
    let live: LiveDecksModel

    var body: some View {
        PhaseMeterPreview(type: model.settings.phaseMeterType, live: LivePreviewSource.meter(model: model, live: live),
                          isPaused: !model.isVisible)
    }
}

private struct LivePeekControl: View {
    let model: MainWindowModel
    let live: LiveDecksModel
    let source: LivePreviewSource
    @Binding var isPeeking: Bool

    var body: some View {
        PreviewPeekControl(isLive: source.isLive(model: model, live: live), isPeeking: $isPeeking)
    }
}

private enum LivePreviewSource {
    case waveform
    case phaseMeter

    func isLive(model: MainWindowModel, live: LiveDecksModel) -> Bool {
        switch self {
        case .waveform: Self.phases(model: model, live: live) != nil
        case .phaseMeter: Self.meter(model: model, live: live) != nil
        }
    }

    static func phases(model: MainWindowModel, live: LiveDecksModel) -> DeckPhases? {
        StatusText.isMirroringDecks(model.status, snapshot: live.snapshot) ? live.waveformPhases : nil
    }

    static func meter(model: MainWindowModel, live: LiveDecksModel) -> PhaseMeterDisplay? {
        let isConnected = StatusText.isMirroringDecks(model.status, snapshot: live.snapshot)
        return isConnected && model.settings.isPhaseMeterEnabled ? live.previewMeter : nil
    }
}
