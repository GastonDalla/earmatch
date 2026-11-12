import SwiftUI
import EarmatchCore

struct MainWindowView: View {
    let model: MainWindowModel
    let live: LiveDecksModel
    let practice: PracticeController
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        Group {
            if model.isLocked {
                PermissionsGate(model: model)
                    .transition(.opacity)
            } else {
                panes
                    .transition(.opacity)
            }
        }
        .animation(WindowTheme.motion(reduceMotion, duration: 0.3), value: model.isLocked)
        .id(model.language)
        .frame(minWidth: MainWindowMetrics.minimumSize.width, minHeight: MainWindowMetrics.minimumSize.height)
        .background(WindowTheme.background)
        .environment(\.colorScheme, .dark)
        .environment(\.locale, AppLanguage.locale)
        .environment(\.showsFocusRing, model.isKeyboardNavigating)
        .task { await practice.loadHistory() }
    }

    private var panes: some View {
        HStack(spacing: 0) {
            MainWindowSidebar(model: model)
            Rectangle().fill(WindowTheme.hairline).frame(width: 1)
            Group {
                switch model.pane {
                case .live: LivePane(model: model, live: live)
                case .overlay: OverlayPane(model: model, live: live)
                case .practice: PracticePane(model: model, practice: practice)
                case .setup: SetupPane(model: model)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        }
    }
}
