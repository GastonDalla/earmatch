import SwiftUI
import EarmatchCore

struct MainWindowView: View {
    let model: MainWindowModel
    let live: LiveDecksModel
    let practice: PracticeController

    var body: some View {
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
        .id(model.language)
        .frame(minWidth: MainWindowMetrics.minimumSize.width, minHeight: MainWindowMetrics.minimumSize.height)
        .background(WindowTheme.background)
        .environment(\.colorScheme, .dark)
        .environment(\.locale, AppLanguage.locale)
        .environment(\.showsFocusRing, model.isKeyboardNavigating)
        .task { await practice.loadHistory() }
    }
}
