import SwiftUI
import EarmatchCore

struct MainWindowSidebar: View {
    let model: MainWindowModel
    @FocusState private var isListFocused: Bool
    @Environment(\.showsFocusRing) private var showsFocusRing
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @Namespace private var highlight

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            SidebarBrand(versionLabel: model.versionLabel)
                .padding(.top, 48)
                .padding(.bottom, Spacing.large)
            VStack(spacing: 2) {
                ForEach(MainWindowPane.allCases, id: \.self) { pane in
                    SidebarRow(pane: pane,
                               isSelected: pane == model.pane,
                               needsAttention: pane == .setup && model.arePermissionsMissing,
                               showsRing: isListFocused && showsFocusRing && pane == model.pane,
                               highlight: highlight,
                               select: { select(pane) })
                }
            }
            .focusable()
            .focused($isListFocused)
            .focusEffectDisabled()
            .onKeyPress(keys: [.upArrow, .downArrow], phases: [.down, .repeat]) { press in
                select(model.pane.moved(by: press.key == .upArrow ? -1 : 1))
                return .handled
            }
            .accessibilityElement(children: .contain)
            .accessibilityLabel(WindowText.sections)
            Spacer(minLength: Spacing.medium)
            SidebarStatus(status: model.windowStatus, detail: StatusText.connection(model.status).text)
        }
        .padding(.horizontal, 12)
        .padding(.bottom, Spacing.medium)
        .frame(width: MainWindowMetrics.sidebarWidth)
        .frame(maxHeight: .infinity, alignment: .top)
        .background(WindowTheme.sidebar)
        .defaultFocus($isListFocused, true)
    }

    private func select(_ pane: MainWindowPane) {
        withAnimation(WindowTheme.motion(reduceMotion, duration: 0.18)) { model.select(pane) }
    }
}

private struct SidebarBrand: View {
    let versionLabel: String

    var body: some View {
        HStack(spacing: 10) {
            Image(nsImage: NSApp.applicationIconImage)
                .resizable()
                .interpolation(.high)
                .frame(width: 32, height: 32)
                .accessibilityHidden(true)
            VStack(alignment: .leading, spacing: 1) {
                Text(verbatim: "Earmatch")
                    .font(.system(size: 15, weight: .bold))
                    .foregroundStyle(WindowTheme.primaryText)
                Text(versionLabel)
                    .font(.system(size: 11, design: .monospaced))
                    .foregroundStyle(WindowTheme.secondaryText)
            }
        }
        .padding(.horizontal, 6)
        .accessibilityElement(children: .combine)
    }
}

private struct SidebarRow: View {
    let pane: MainWindowPane
    let isSelected: Bool
    let needsAttention: Bool
    let showsRing: Bool
    let highlight: Namespace.ID
    let select: () -> Void

    private let shape = RoundedRectangle(cornerRadius: 9, style: .continuous)

    var body: some View {
        HStack(spacing: 0) {
            Image(systemName: pane.symbolName)
                .font(.system(size: 13.5, weight: .semibold))
                .foregroundStyle(isSelected ? WindowTheme.accent : WindowTheme.secondaryText)
                .frame(width: 20)
            Text(pane.title)
                .font(TypeScale.bodyStrong)
                .foregroundStyle(isSelected ? WindowTheme.primaryText : WindowTheme.primaryText.opacity(0.75))
                .lineLimit(1)
                .minimumScaleFactor(0.85)
                .padding(.leading, 10)
            Spacer(minLength: 6)
            if needsAttention {
                Circle().fill(WindowTheme.waiting).frame(width: 7, height: 7)
                    .padding(.trailing, 6)
            }
            Text(verbatim: "⌘\(pane.shortcutNumber)")
                .font(.system(size: 11, weight: .medium, design: .rounded))
                .foregroundStyle(WindowTheme.sectionText)
        }
        .padding(.horizontal, 10)
        .frame(height: 34)
        .background {
            if isSelected {
                shape.fill(WindowTheme.accent.opacity(0.13))
                    .matchedGeometryEffect(id: "selection", in: highlight)
            }
        }
        .overlay {
            shape.inset(by: -1)
                .strokeBorder(WindowTheme.accent, lineWidth: 2)
                .opacity(showsRing ? 1 : 0)
                .allowsHitTesting(false)
        }
        .contentShape(shape)
        .onTapGesture(perform: select)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(pane.title)
        .accessibilityValue(needsAttention ? MainWindowStatus.needsPermission.title : "")
        .accessibilityHint(WindowText.shortcut(pane.shortcutNumber))
        .accessibilityAddTraits(isSelected ? [.isButton, .isSelected] : .isButton)
        .accessibilityAction(.default, select)
    }
}

private struct SidebarStatus: View {
    let status: MainWindowStatus
    let detail: String
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        let colour = WindowTheme.color(for: status.tone)
        HStack(spacing: Spacing.small) {
            Circle().fill(colour).frame(width: 8, height: 8)
            Text(status.title)
                .font(TypeScale.caption)
                .foregroundStyle(WindowTheme.primaryText.opacity(0.85))
                .lineLimit(2)
                .fixedSize(horizontal: false, vertical: true)
                .contentTransition(.opacity)
            Spacer(minLength: 0)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, Spacing.small)
        .surface(cornerRadius: 10, fill: colour.opacity(0.10), stroke: colour.opacity(0.22))
        .animation(WindowTheme.motion(reduceMotion), value: status)
        .help(detail)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(WindowText.status(status.title))
        .accessibilityValue(detail)
    }
}
