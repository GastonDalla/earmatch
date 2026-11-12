import SwiftUI
import EarmatchCore

/// The window's only screen while a permission is missing: no sidebar, options, live decks or hotkeys until both are
/// allowed. It reuses the Setup pane's permissions card, so the rows, Open Settings and Restart behave the same.
struct PermissionsGate: View {
    let model: MainWindowModel

    private static let columnWidth: CGFloat = 560

    var body: some View {
        GeometryReader { proxy in
            ScrollView {
                VStack(spacing: 0) {
                    identity
                    VStack(spacing: Spacing.small) {
                        Text(WindowText.gateTitle)
                            .font(TypeScale.paneTitle)
                            .foregroundStyle(WindowTheme.primaryText)
                            .multilineTextAlignment(.center)
                            .accessibilityAddTraits(.isHeader)
                        Text(WindowText.gateExplanation)
                            .font(TypeScale.body)
                            .foregroundStyle(WindowTheme.secondaryText)
                            .multilineTextAlignment(.center)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                    .padding(.top, Spacing.large)
                    VStack(alignment: .leading, spacing: 12) {
                        PermissionsCard(model: model)
                        CardStatusRow(line: StatusText.permissions(isWaiting: model.isWaitingForPermissions))
                            .padding(.horizontal, Spacing.medium)
                    }
                    .padding(.top, Spacing.large)
                    VStack(spacing: Spacing.medium) {
                        ActionButton(kind: .pill, accessibilityText: WindowText.quit, action: { NSApp.terminate(nil) }) {
                            Text(WindowText.quit)
                        }
                        CreditLines()
                    }
                    .padding(.top, Spacing.extraLarge)
                }
                .frame(maxWidth: Self.columnWidth)
                .padding(.horizontal, Spacing.large)
                .padding(.top, 48)
                .padding(.bottom, Spacing.large)
                .frame(maxWidth: .infinity, minHeight: proxy.size.height)
            }
            .scrollIndicators(.automatic)
        }
    }

    private var identity: some View {
        VStack(spacing: 0) {
            Image(nsImage: NSApp.applicationIconImage)
                .resizable()
                .interpolation(.high)
                .frame(width: 64, height: 64)
                .accessibilityHidden(true)
            Text(verbatim: "Earmatch")
                .font(TypeScale.title)
                .foregroundStyle(WindowTheme.primaryText)
                .padding(.top, Spacing.small)
            Text(model.versionLabel)
                .font(TypeScale.status)
                .foregroundStyle(WindowTheme.secondaryText)
                .padding(.top, Spacing.tight)
        }
        .accessibilityElement(children: .combine)
    }
}
