import SwiftUI
import EarmatchCore

struct LivePane: View {
    let model: MainWindowModel
    let live: LiveDecksModel
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        PaneScaffold(title: MainWindowPane.live.title) {
            ConnectionChip(line: StatusText.connection(model.status))
        } content: {
            VStack(alignment: .leading, spacing: 12) {
                if model.arePermissionsMissing {
                    PermissionBanner(openSetup: { model.select(.setup) })
                        .transition(.opacity)
                }
                HStack(alignment: .top, spacing: 12) {
                    EnableCard(model: model)
                    BoothCard(model: model)
                        .opacity(model.settings.isEnabled ? 1 : 0.55)
                }
                .fixedSize(horizontal: false, vertical: true)
                LiveDecksPanel(model: model, live: live)
                Spacer(minLength: 0)
                HStack(spacing: Spacing.large) {
                    HotkeyHint(key: WindowText.rightOptionKey, text: WindowText.peekHint)
                    HotkeyHint(key: "⌃⌥⌘E", text: WindowText.toggleHint)
                    Spacer(minLength: 0)
                }
                .accessibilityElement(children: .combine)
                .accessibilityLabel(WindowText.hotkeysSpoken)
            }
            .animation(WindowTheme.motion(reduceMotion, duration: 0.25), value: model.arePermissionsMissing)
            .animation(WindowTheme.motion(reduceMotion), value: model.settings.isEnabled)
        }
    }
}

struct ConnectionChip: View {
    let line: StatusLine
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        let colour = WindowTheme.color(for: line.tone)
        HStack(spacing: Spacing.small) {
            Circle().fill(colour).frame(width: 8, height: 8)
            Text(line.text)
                .font(.system(size: 13))
                .foregroundStyle(WindowTheme.primaryText.opacity(0.88))
                .lineLimit(1)
                .truncationMode(.middle)
                .contentTransition(.opacity)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 5)
        .surface(cornerRadius: 10, fill: colour.opacity(0.10), stroke: colour.opacity(0.22))
        .animation(WindowTheme.motion(reduceMotion), value: line)
        .help(line.text)
        .accessibilityElement(children: .combine)
    }
}

private struct PermissionBanner: View {
    let openSetup: () -> Void

    var body: some View {
        HStack(spacing: Spacing.medium) {
            Image(systemName: "exclamationmark.shield.fill")
                .font(.system(size: 17, weight: .semibold))
                .foregroundStyle(WindowTheme.waiting)
                .accessibilityHidden(true)
            Text(WindowText.permissionMissing)
                .font(TypeScale.bodyStrong)
                .foregroundStyle(WindowTheme.primaryText)
                .fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: Spacing.small)
            ActionButton(kind: .accent, accessibilityText: WindowText.openSetup, action: openSetup) {
                Text(WindowText.openSetup)
            }
        }
        .padding(.horizontal, Spacing.medium)
        .padding(.vertical, 10)
        .surface(cornerRadius: 12, fill: WindowTheme.waiting.opacity(0.12), stroke: WindowTheme.waiting.opacity(0.3))
        .accessibilityElement(children: .contain)
    }
}

private struct EnableCard: View {
    let model: MainWindowModel

    var body: some View {
        let status = model.windowStatus
        PaneCard(fillsHeight: true) {
            OptionSwitchRow(title: WindowText.enable,
                            subtitle: WindowText.enableHelp,
                            hint: WindowText.enableHint,
                            isOn: model.binding(\.isEnabled))
            Spacer(minLength: 0)
            CardStatusRow(line: StatusLine(tone: status.tone, text: status.title))
        }
        .help(WindowText.enableHelp)
    }
}

private struct BoothCard: View {
    let model: MainWindowModel

    var body: some View {
        let selected = BoothPreset.selected(in: model.settings)
        PaneCard(title: OptionText.boothSectionTitle, fillsHeight: true) {
            VStack(alignment: .leading, spacing: 6) {
                OptionPicker(title: OptionText.boothTitle,
                             options: BoothPreset.allCases.map { ($0.displayName, Optional($0)) },
                             explanation: { $0?.explanation ?? BoothPreset.customExplanation },
                             selection: model.boothPreset)
                ExplanationLine(text: selected?.explanation ?? BoothPreset.customExplanation)
            }
            Text(OptionText.boothSubtitle)
                .font(TypeScale.caption)
                .foregroundStyle(WindowTheme.sectionText)
                .fixedSize(horizontal: false, vertical: true)
                .padding(.leading, Spacing.tight)
        }
    }
}
