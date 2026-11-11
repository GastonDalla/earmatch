import SwiftUI
import EarmatchCore

struct PaneScaffold<Accessory: View, Content: View>: View {
    let title: String
    @ViewBuilder var accessory: Accessory
    @ViewBuilder var content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.medium) {
            HStack(alignment: .center, spacing: Spacing.medium) {
                Text(title)
                    .font(TypeScale.paneTitle)
                    .foregroundStyle(WindowTheme.primaryText)
                    .lineLimit(1)
                    .accessibilityAddTraits(.isHeader)
                Spacer(minLength: Spacing.medium)
                accessory
            }
            .frame(minHeight: 30)
            content
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        }
        .padding(.horizontal, Spacing.large)
        .padding(.top, 14)
        .padding(.bottom, 20)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }
}

extension PaneScaffold where Accessory == EmptyView {
    init(title: String, @ViewBuilder content: () -> Content) {
        self.init(title: title, accessory: { EmptyView() }, content: content)
    }
}

struct PaneCard<Accessory: View, Content: View>: View {
    var title: String?
    var fillsHeight = false
    @ViewBuilder var accessory: Accessory
    @ViewBuilder var content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            if let title {
                HStack(spacing: Spacing.small) {
                    CardTitle(text: title)
                    Spacer(minLength: Spacing.small)
                    accessory
                }
                .frame(minHeight: 18)
            }
            content
        }
        .padding(.horizontal, Spacing.medium)
        .padding(.vertical, 12)
        .frame(maxWidth: .infinity, maxHeight: fillsHeight ? .infinity : nil, alignment: .topLeading)
        .surface(cornerRadius: 14, fill: WindowTheme.card, stroke: WindowTheme.hairline)
    }
}

extension PaneCard where Accessory == EmptyView {
    init(title: String? = nil, fillsHeight: Bool = false, @ViewBuilder content: () -> Content) {
        self.init(title: title, fillsHeight: fillsHeight, accessory: { EmptyView() }, content: content)
    }
}

struct CardTitle: View {
    let text: String

    var body: some View {
        Text(text)
            .font(TypeScale.section)
            .tracking(2)
            .foregroundStyle(WindowTheme.sectionText)
            .lineLimit(1)
            .accessibilityAddTraits(.isHeader)
    }
}

struct PaneColumns<Leading: View, Trailing: View>: View {
    @ViewBuilder var leading: Leading
    @ViewBuilder var trailing: Trailing

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            VStack(spacing: 12) { leading }
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
            VStack(spacing: 12) { trailing }
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        }
    }
}

struct HotkeyHint: View {
    let key: String
    let text: String

    var body: some View {
        HStack(spacing: Spacing.small) {
            KeyCap(text: key)
            Text(text)
                .font(TypeScale.caption)
                .foregroundStyle(WindowTheme.secondaryText)
                .lineLimit(1)
                .minimumScaleFactor(0.9)
        }
    }
}

extension MainWindowPane {
    var symbolName: String {
        switch self {
        case .live: "dot.radiowaves.left.and.right"
        case .overlay: "rectangle.on.rectangle"
        case .practice: "metronome"
        case .setup: "gearshape"
        }
    }
}
