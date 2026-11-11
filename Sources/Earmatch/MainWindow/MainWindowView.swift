import SwiftUI
import EarmatchCore

struct MainWindowView: View {
    @ObservedObject var model: MainWindowModel
    let live: LiveDecksModel
    let practice: PracticeController
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        ScrollView {
            VStack(spacing: 0) {
                HeaderView(model: model)
                Rectangle().fill(WindowTheme.hairline).frame(height: 1)
                VStack(alignment: .leading, spacing: 0) {
                    if model.isPermissionsCardShown {
                        VStack(alignment: .leading, spacing: 0) {
                            CardSectionTitle(text: WindowText.permissions)
                            PermissionsCard(model: model)
                        }
                        .transition(.opacity.combined(with: .move(edge: .top)))
                    }
                    CardSectionTitle(text: WindowText.liveDecks)
                    LiveDecksPanel(model: model, live: live)
                    Group {
                        CardSectionTitle(text: OptionText.boothSectionTitle)
                        BoothCard(model: model)
                        CardSectionTitle(text: WindowText.bpmPrecisionSection)
                        BPMPrecisionCard(model: model)
                        CardSectionTitle(text: WindowText.waveformSection)
                        WaveformCard(model: model, live: live)
                        CardSectionTitle(text: WindowText.phaseMeterSection)
                        PhaseMeterCard(model: model, live: live)
                        CardSectionTitle(text: PracticeStrings.sectionTitle)
                        MixTrainerSwitchCard(model: model)
                            .padding(.bottom, Spacing.small)
                        PracticePanel(practice: practice)
                    }
                    .opacity(model.settings.isEnabled ? 1 : 0.55)
                }
                .padding(.horizontal, Spacing.large)
                .padding(.bottom, Spacing.extraLarge)
                .animation(WindowTheme.motion(reduceMotion, duration: 0.25), value: model.isPermissionsCardShown)
                .animation(WindowTheme.motion(reduceMotion), value: model.settings.isEnabled)
                Rectangle().fill(WindowTheme.hairline).frame(height: 1)
                FooterView(model: model)
            }
        }
        .scrollIndicators(.automatic)
        .background(WindowTheme.background)
        .environment(\.colorScheme, .dark)
        .environment(\.locale, AppLanguage.locale)
        .environment(\.showsFocusRing, model.isKeyboardNavigating)
    }
}

private struct HeaderView: View {
    @ObservedObject var model: MainWindowModel

    var body: some View {
        VStack(spacing: 0) {
            Image(nsImage: NSApp.applicationIconImage)
                .resizable()
                .interpolation(.high)
                .frame(width: 64, height: 64)
                .accessibilityHidden(true)
            Text(verbatim: "Earmatch")
                .font(TypeScale.title)
                .foregroundStyle(WindowTheme.primaryText)
                .padding(.top, Spacing.medium)
                .accessibilityAddTraits(.isHeader)
            Text(model.versionLabel)
                .font(TypeScale.status)
                .foregroundStyle(WindowTheme.secondaryText)
                .padding(.top, Spacing.tight)
            ConnectionChip(line: StatusText.connection(model.status))
                .padding(.top, Spacing.medium)
            HStack(spacing: Spacing.medium) {
                Text(WindowText.enable).font(TypeScale.headline).foregroundStyle(WindowTheme.primaryText)
                Toggle(WindowText.enable, isOn: model.binding(\.isEnabled))
                    .toggleStyle(AccentSwitchStyle())
                    .accessibilityHint(WindowText.enableHint)
            }
            .help(WindowText.enableHelp)
            .padding(.top, Spacing.large)
        }
        .padding(.top, Spacing.large)
        .padding(.bottom, Spacing.large)
        .padding(.horizontal, Spacing.large)
        .frame(maxWidth: .infinity)
    }
}

private struct ConnectionChip: View {
    let line: StatusLine
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        let colour = WindowTheme.color(for: line.tone)
        HStack(spacing: Spacing.small) {
            Circle().fill(colour).frame(width: 8, height: 8)
            Text(line.text)
                .font(.system(size: 13.5))
                .foregroundStyle(WindowTheme.primaryText.opacity(0.88))
                .multilineTextAlignment(.center)
                .contentTransition(.opacity)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 6)
        .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(colour.opacity(0.10)))
        .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).strokeBorder(colour.opacity(0.22)))
        .animation(WindowTheme.motion(reduceMotion), value: line)
        .accessibilityElement(children: .combine)
    }
}

private struct PermissionsCard: View {
    @ObservedObject var model: MainWindowModel

    var body: some View {
        OptionCard {
            PermissionRow(
                title: WindowText.accessibility,
                reason: WindowText.accessibilityReason,
                isGranted: model.isAccessibilityGranted,
                open: Permissions.requestAccessibility
            )
            Rectangle().fill(WindowTheme.hairline).frame(height: 1)
            PermissionRow(
                title: WindowText.screenRecording,
                reason: WindowText.screenRecordingReason,
                isGranted: model.isScreenRecordingGranted,
                open: Permissions.requestScreenRecording
            )
            HStack(alignment: .center, spacing: Spacing.small) {
                Text(WindowText.reopenHint)
                    .font(TypeScale.caption)
                    .foregroundStyle(WindowTheme.secondaryText)
                    .fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: Spacing.small)
                if Permissions.canRelaunch {
                    ActionButton(kind: .pill, accessibilityText: WindowText.restart, action: Permissions.relaunch) {
                        Text(WindowText.restart)
                    }
                }
            }
        }
    }
}

private struct PermissionRow: View {
    let title: String
    let reason: String
    let isGranted: Bool
    let open: () -> Void
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.tight) {
            HStack(spacing: Spacing.small) {
                Image(systemName: isGranted ? "checkmark.circle.fill" : "circle.dashed")
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundStyle(isGranted ? WindowTheme.granted : WindowTheme.waiting)
                    .frame(width: 18)
                    .accessibilityHidden(true)
                Text(title).font(TypeScale.headline).foregroundStyle(WindowTheme.primaryText)
                Spacer(minLength: Spacing.small)
                if isGranted {
                    Text(WindowText.allowed)
                        .font(TypeScale.bodyStrong)
                        .foregroundStyle(WindowTheme.granted)
                        .transition(.opacity)
                } else {
                    ActionButton(kind: .accent, accessibilityText: WindowText.openSettingsToAllow(title), action: open) {
                        Text(WindowText.openSettings)
                    }
                    .transition(.opacity)
                }
            }
            Text(reason)
                .font(TypeScale.caption)
                .foregroundStyle(WindowTheme.secondaryText)
                .fixedSize(horizontal: false, vertical: true)
                .padding(.leading, 26)
        }
        .animation(WindowTheme.motion(reduceMotion, duration: 0.25), value: isGranted)
        .accessibilityElement(children: .contain)
        .accessibilityLabel(WindowText.permissionSummary(title, isGranted: isGranted, reason: reason))
    }
}

private struct BPMPrecisionCard: View {
    @ObservedObject var model: MainWindowModel

    var body: some View {
        let settings = model.settings
        OptionCard {
            OptionSwitchRow(title: WindowText.limitBPM,
                            subtitle: WindowText.limitBPMSubtitle,
                            isOn: model.binding(\.isBPMLimitEnabled))
            VStack(alignment: .leading, spacing: Spacing.small) {
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
                        .background(RoundedRectangle(cornerRadius: 3).fill(Color.black))
                        .overlay(RoundedRectangle(cornerRadius: 3).strokeBorder(WindowTheme.accent, lineWidth: 1.5))
                }
            }
            .font(.system(size: 15, weight: .semibold, design: .monospaced))
            .accessibilityElement(children: .ignore)
            .accessibilityLabel(sample.visible.isEmpty ? WindowText.sampleHidden : WindowText.sample(reading: sample.visible))
        }
    }
}

private struct WaveformCard: View {
    @ObservedObject var model: MainWindowModel
    let live: LiveDecksModel
    @State private var isPeeking = false

    var body: some View {
        let mode = model.settings.waveformMode
        OptionCard {
            VStack(alignment: .leading, spacing: Spacing.small) {
                OptionPicker(title: WindowText.waveform, options: WaveformMode.allCases.map { ($0.displayName, $0) },
                             explanation: \.explanation, selection: model.binding(\.waveformMode))
                ExplanationLine(text: mode.explanation)
            }
            LiveWaveformPreview(model: model, live: live, mode: isPeeking ? .normal : mode, isPeeking: $isPeeking)
            CardStatusRow(line: StatusText.waveform(model.status))
        }
    }
}

private struct LiveWaveformPreview: View {
    @ObservedObject var model: MainWindowModel
    @ObservedObject var live: LiveDecksModel
    let mode: WaveformMode
    @Binding var isPeeking: Bool

    var body: some View {
        let isConnected = StatusText.isMirroringDecks(model.status, snapshot: live.snapshot)
        let phases = isConnected ? live.waveformPhases : nil
        PreviewBox(height: 64, isLive: phases != nil, isPeeking: $isPeeking) {
            WaveformPreview(mode: mode, live: phases, isPaused: !model.isVisible)
        }
    }
}

private struct PhaseMeterCard: View {
    @ObservedObject var model: MainWindowModel
    let live: LiveDecksModel
    @State private var isPeeking = false

    var body: some View {
        let settings = model.settings
        OptionCard {
            OptionSwitchRow(title: WindowText.phaseMeter,
                            subtitle: WindowText.phaseMeterSubtitle,
                            isOn: model.binding(\.isPhaseMeterEnabled))
            VStack(alignment: .leading, spacing: Spacing.small) {
                OptionPicker(title: WindowText.phaseMeterType, options: PhaseMeterType.allCases.map { ($0.displayName, $0) },
                             explanation: \.explanation, selection: model.binding(\.phaseMeterType))
                ExplanationLine(text: settings.phaseMeterType.explanation)
            }
            .opacity(settings.isPhaseMeterEnabled ? 1 : 0.45)
            VStack(alignment: .leading, spacing: Spacing.small) {
                Text(OptionText.counterTitle).font(TypeScale.bodyStrong).foregroundStyle(WindowTheme.secondaryText)
                    .padding(.leading, Spacing.tight)
                    .accessibilityHidden(true)
                OptionPicker(title: OptionText.counterTitle, options: CounterStyle.allCases.map { ($0.displayName, $0) },
                             explanation: { WindowText.counterHelp($0) }, selection: model.binding(\.counterStyle))
                ExplanationLine(text: settings.counterStyle.explanation)
            }
            .opacity(settings.isPhaseMeterEnabled ? 1 : 0.45)
            LivePhaseMeterPreview(model: model, live: live, isPeeking: $isPeeking)
            CardStatusRow(line: StatusText.phaseMeter(model.status))
        }
    }
}

private struct LivePhaseMeterPreview: View {
    @ObservedObject var model: MainWindowModel
    @ObservedObject var live: LiveDecksModel
    @Binding var isPeeking: Bool

    var body: some View {
        let settings = model.settings
        let isConnected = StatusText.isMirroringDecks(model.status, snapshot: live.snapshot)
        let meter = isConnected && settings.isPhaseMeterEnabled ? live.previewMeter : nil
        PreviewBox(height: 58, isLive: meter != nil, isPeeking: $isPeeking) {
            if !isPeeking {
                PhaseMeterPreview(type: settings.phaseMeterType, live: meter, isPaused: !model.isVisible)
                    .opacity(settings.isPhaseMeterEnabled ? 1 : 0.3)
            }
        }
    }
}

private struct BoothCard: View {
    @ObservedObject var model: MainWindowModel

    var body: some View {
        let selected = BoothPreset.selected(in: model.settings)
        OptionCard {
            VStack(alignment: .leading, spacing: Spacing.small) {
                Text(OptionText.boothSubtitle).font(TypeScale.caption).foregroundStyle(WindowTheme.secondaryText)
                    .padding(.leading, Spacing.tight)
                OptionPicker(title: OptionText.boothTitle,
                             options: BoothPreset.allCases.map { ($0.displayName, Optional($0)) },
                             explanation: { $0?.explanation ?? BoothPreset.customExplanation },
                             selection: model.boothPreset)
                ExplanationLine(text: selected?.explanation ?? BoothPreset.customExplanation)
            }
        }
    }
}

private struct MixTrainerSwitchCard: View {
    @ObservedObject var model: MainWindowModel

    var body: some View {
        OptionCard {
            OptionSwitchRow(title: OptionText.mixTrainerTitle,
                            subtitle: OptionText.mixTrainerSubtitle,
                            isOn: model.binding(\.isMixTrainerEnabled))
        }
    }
}

private struct FooterView: View {
    @ObservedObject var model: MainWindowModel
    @State private var didCopy = false
    @Environment(\.openURL) private var openURL

    var body: some View {
        VStack(spacing: Spacing.medium) {
            Grid(alignment: .leading, horizontalSpacing: Spacing.small, verticalSpacing: Spacing.small) {
                GridRow {
                    KeyCap(text: WindowText.rightOptionKey).gridColumnAlignment(.trailing)
                    Text(WindowText.peekHint)
                }
                GridRow {
                    KeyCap(text: "⌃⌥⌘E")
                    Text(WindowText.toggleHint)
                }
            }
            .font(TypeScale.caption)
            .foregroundStyle(WindowTheme.secondaryText)
            .accessibilityElement(children: .combine)
            .accessibilityLabel(WindowText.hotkeysSpoken)

            HStack(spacing: Spacing.small) {
                ActionButton(kind: .pill, accessibilityText: WindowText.copyDiagnosticsHelp, action: copy) {
                    Label(didCopy ? WindowText.copied : WindowText.copyDiagnostics, systemImage: didCopy ? "checkmark" : "doc.on.clipboard")
                        .contentTransition(.opacity)
                }
                ActionButton(kind: .pill, accessibilityText: WindowText.quit, action: { NSApp.terminate(nil) }) {
                    Text(WindowText.quit)
                }
            }

            VStack(spacing: Spacing.tight) {
                ActionButton(kind: .link, accessibilityText: WindowText.creditHelp, action: { openURL(Credit.url) }) {
                    HStack(spacing: 5) {
                        (Text(WindowText.credit.before).foregroundStyle(WindowTheme.secondaryText)
                            + Text(Credit.author).fontWeight(.semibold).foregroundStyle(WindowTheme.accent)
                            + Text(WindowText.credit.after).foregroundStyle(WindowTheme.secondaryText))
                        Image(systemName: "arrow.up.right")
                            .font(.system(size: 9, weight: .bold))
                            .foregroundStyle(WindowTheme.accent.opacity(0.8))
                    }
                    .font(TypeScale.body)
                }
                Text(WindowText.independence)
                    .font(.system(size: 11.5))
                    .foregroundStyle(WindowTheme.sectionText)
                    .multilineTextAlignment(.center)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .padding(.top, Spacing.large)
        .padding(.bottom, Spacing.large)
        .padding(.horizontal, Spacing.large)
        .frame(maxWidth: .infinity)
    }

    private func copy() {
        model.onCopyDiagnostics()
        withAnimation(.easeOut(duration: 0.15)) { didCopy = true }
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.6) {
            withAnimation(.easeOut(duration: 0.15)) { didCopy = false }
        }
    }
}

private enum WindowText {
    static let permissions = L10n.text("PERMISSIONS")
    static let liveDecks = L10n.text("LIVE DECKS")
    static let bpmPrecisionSection = L10n.text("BPM PRECISION")
    static let waveformSection = L10n.text("WAVEFORM")
    static let phaseMeterSection = L10n.text("PHASE METER")
    static let enable = L10n.text("Enable Earmatch")
    static let enableHint = L10n.text("Turns every overlay on rekordbox on or off. Shortcut: Control Option Command E.")
    static let enableHelp = L10n.text("Turns every overlay on rekordbox on or off (⌃⌥⌘E)")
    static let accessibility = L10n.text("Accessibility")
    static let accessibilityReason = L10n.text("Finds rekordbox's decks, labels and window position.")
    static let screenRecording = L10n.text("Screen Recording")
    static let screenRecordingReason = L10n.text("Sees rekordbox's waveforms and digits; never saved.")
    static let reopenHint = L10n.text("If macOS asks, quit and reopen Earmatch.")
    static let restart = L10n.text("Restart Earmatch")
    static let allowed = L10n.text("Allowed")
    static let openSettings = L10n.text("Open Settings")
    static let limitBPM = L10n.text("Limit BPM display")
    static let limitBPMSubtitle = L10n.text("Covers the jog BPM's digits, so you match by ear")
    static let bpmPrecision = L10n.text("BPM precision")
    static let sampleHidden = L10n.text("Sample: BPM hidden")
    static let waveform = L10n.text("Waveform")
    static let phaseMeter = L10n.text("Phase meter")
    static let phaseMeterSubtitle = L10n.text("Pioneer-style beat meter on each deck")
    static let phaseMeterType = L10n.text("Phase meter type")
    static let rightOptionKey = L10n.text("Right ⌥")
    static let peekHint = L10n.text("Hold to see rekordbox unchanged")
    static let toggleHint = L10n.text("Turn Earmatch on or off")
    static let hotkeysSpoken = L10n.text("Hotkeys: hold Right Option to see rekordbox unchanged; Control Option Command E turns Earmatch on or off")
    static let copyDiagnostics = L10n.text("Copy Diagnostics")
    static let copyDiagnosticsHelp = L10n.text("Copy Diagnostics: copies a plain-text report (versions, permissions, status, settings) to attach to a bug report")
    static let copied = L10n.text("Copied")
    static let quit = L10n.text("Quit Earmatch")
    static let credit: (before: String, after: String) = {
        guard let range = Credit.line.range(of: Credit.author) else { return (Credit.line + " ", "") }
        return (String(Credit.line[..<range.lowerBound]), String(Credit.line[range.upperBound...]))
    }()
    static let creditHelp = L10n.format("Made by %@. Opens %@ in your browser", Credit.author, Credit.displayURL)
    static let independence = L10n.text("Independent utility. Not affiliated with AlphaTheta, Pioneer DJ or rekordbox.")

    private static let sampleFormat = L10n.template("Sample: BPM reads %@")

    static func sample(reading visible: String) -> String {
        String(format: sampleFormat, visible)
    }

    static func openSettingsToAllow(_ permission: String) -> String {
        L10n.format("Open Settings to allow %@", permission)
    }

    static func permissionSummary(_ permission: String, isGranted: Bool, reason: String) -> String {
        isGranted ? L10n.format("%@: allowed. %@", permission, reason) : L10n.format("%@: not allowed. %@", permission, reason)
    }

    static func counterHelp(_ style: CounterStyle) -> String {
        "\(style.title): \(style.explanation)"
    }
}
