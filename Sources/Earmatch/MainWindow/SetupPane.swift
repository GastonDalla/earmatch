import SwiftUI
import EarmatchCore

struct SetupPane: View {
    let model: MainWindowModel

    var body: some View {
        PaneScaffold(title: MainWindowPane.setup.title) {
            PaneColumns {
                PermissionsCard(model: model)
                LanguageCard(model: model)
                HotkeysCard()
            } trailing: {
                AboutCard(model: model)
            }
        }
    }
}

/// Also the heart of the permissions screen the window shows until both are allowed.
struct PermissionsCard: View {
    let model: MainWindowModel

    var body: some View {
        PaneCard(title: WindowText.permissions) {
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
            Rectangle().fill(WindowTheme.hairline).frame(height: 1)
            HStack(alignment: .center, spacing: Spacing.small) {
                Text(WindowText.reopenHint)
                    .font(TypeScale.caption)
                    .foregroundStyle(WindowTheme.secondaryText)
                    .fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: Spacing.small)
                if Permissions.canRelaunch {
                    ActionButton(kind: .pill, accessibilityText: WindowText.restart, action: Permissions.relaunch) {
                        Text(WindowText.restart).fixedSize()
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

private struct LanguageCard: View {
    let model: MainWindowModel

    var body: some View {
        PaneCard(title: WindowText.languageSection) {
            VStack(alignment: .leading, spacing: 6) {
                OptionPicker(title: WindowText.language, options: AppLanguageChoice.allCases.map { ($0.label, $0) },
                             explanation: \.explanation, selection: model.languageBinding)
                ExplanationLine(text: model.language.explanation)
            }
        }
    }
}

private struct HotkeysCard: View {
    var body: some View {
        PaneCard(title: WindowText.hotkeysSection) {
            Grid(alignment: .leading, horizontalSpacing: Spacing.medium, verticalSpacing: 10) {
                GridRow {
                    KeyCap(text: WindowText.rightOptionKey).gridColumnAlignment(.trailing)
                    Text(WindowText.peekHint)
                }
                GridRow {
                    KeyCap(text: "⌃⌥⌘E")
                    Text(WindowText.toggleHint)
                }
                GridRow {
                    KeyCap(text: "⌘1 – ⌘4")
                    Text(WindowText.switchSectionsHint)
                }
            }
            .font(TypeScale.caption)
            .foregroundStyle(WindowTheme.secondaryText)
        }
    }
}

private struct AboutCard: View {
    let model: MainWindowModel
    @State private var didCopy = false
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        PaneCard(fillsHeight: true) {
            VStack(spacing: 0) {
                Image(nsImage: NSApp.applicationIconImage)
                    .resizable()
                    .interpolation(.high)
                    .frame(width: 72, height: 72)
                    .accessibilityHidden(true)
                Text(verbatim: "Earmatch")
                    .font(TypeScale.title)
                    .foregroundStyle(WindowTheme.primaryText)
                    .padding(.top, Spacing.small)
                    .accessibilityAddTraits(.isHeader)
                Text(model.versionLabel)
                    .font(TypeScale.status)
                    .foregroundStyle(WindowTheme.secondaryText)
                    .padding(.top, Spacing.tight)
                VStack(spacing: Spacing.small) {
                    if model.canCheckForUpdates {
                        ActionButton(kind: .pill, accessibilityText: WindowText.checkForUpdates, action: model.onCheckForUpdates) {
                            Label(WindowText.checkForUpdates, systemImage: "arrow.down.circle")
                        }
                    }
                    ActionButton(kind: .pill, accessibilityText: WindowText.copyDiagnosticsHelp, action: copy) {
                        Label(didCopy ? WindowText.copied : WindowText.copyDiagnostics, systemImage: didCopy ? "checkmark" : "doc.on.clipboard")
                            .contentTransition(.opacity)
                    }
                    ActionButton(kind: .pill, accessibilityText: WindowText.quit, action: { NSApp.terminate(nil) }) {
                        Text(WindowText.quit)
                    }
                }
                .padding(.top, Spacing.large)
                Spacer(minLength: Spacing.large)
                CreditLines()
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .padding(.vertical, Spacing.small)
        }
        .task(id: didCopy) {
            guard didCopy else { return }
            try? await Task.sleep(for: .seconds(1.6))
            guard !Task.isCancelled else { return }
            withAnimation(WindowTheme.motion(reduceMotion, duration: 0.15)) { didCopy = false }
        }
    }

    private func copy() {
        model.onCopyDiagnostics()
        withAnimation(WindowTheme.motion(reduceMotion, duration: 0.15)) { didCopy = true }
    }
}

/// The credit link and the independence line, shown in the About card and on the permissions screen.
struct CreditLines: View {
    @Environment(\.openURL) private var openURL

    var body: some View {
        VStack(spacing: Spacing.tight) {
            ActionButton(kind: .link, accessibilityText: WindowText.creditHelp, action: { openURL(Credit.url) }) {
                HStack(spacing: 5) {
                    creditLine
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

    private var creditLine: Text {
        let before = Text(WindowText.credit.before).foregroundStyle(WindowTheme.secondaryText)
        let author = Text(Credit.author).fontWeight(.semibold).foregroundStyle(WindowTheme.accent)
        let after = Text(WindowText.credit.after).foregroundStyle(WindowTheme.secondaryText)
        let line: LocalizedStringKey = "\(before)\(author)\(after)"
        return Text(line)
    }
}
