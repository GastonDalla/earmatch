import SwiftUI
import EarmatchCore

enum WindowTheme {
    static let accent = Color(red: 0.72, green: 0.95, blue: 0.23)
    static let onAccent = Color(red: 0.07, green: 0.09, blue: 0.05)
    static let waiting = Color(red: 0.96, green: 0.65, blue: 0.14)
    static let off = Color(white: 0.42)
    static let granted = Color(red: 0.30, green: 0.85, blue: 0.42)

    static let background = Color(red: 0.110, green: 0.110, blue: 0.118)
    static let card = Color(red: 0.145, green: 0.145, blue: 0.157)
    static let well = Color(red: 0.086, green: 0.086, blue: 0.094)
    static let hairline = Color.white.opacity(0.08)

    static let primaryText = Color.white.opacity(0.92)
    static let secondaryText = Color.white.opacity(0.58)
    static let sectionText = Color.white.opacity(0.42)

    static let master = Color(red: 240 / 255, green: 140 / 255, blue: 40 / 255)
    static let masterDim = Color(red: 130 / 255, green: 72 / 255, blue: 22 / 255)
    static let deck = Color(red: 60 / 255, green: 140 / 255, blue: 220 / 255)
    static let deckDim = Color(red: 30 / 255, green: 68 / 255, blue: 110 / 255)
    static let deckTwo = Color(red: 70 / 255, green: 200 / 255, blue: 215 / 255)
    static let downbeat = Color(red: 230 / 255, green: 50 / 255, blue: 40 / 255)
    static let cdjOrange = Color(red: 1, green: 115 / 255, blue: 0)
    static let cdjBlue = Color(red: 0, green: 125 / 255, blue: 1)
    static let tickGrey = Color(white: 205 / 255)

    static func color(for tone: StatusTone) -> Color {
        switch tone {
        case .live: accent
        case .waiting: waiting
        case .off: off
        }
    }

    static func motion(_ reduceMotion: Bool, duration: Double = 0.2) -> Animation? {
        reduceMotion ? nil : .easeOut(duration: duration)
    }
}

enum Spacing {
    static let tight: CGFloat = 4
    static let small: CGFloat = 8
    static let medium: CGFloat = 16
    static let large: CGFloat = 24
    static let extraLarge: CGFloat = 32
}

enum TypeScale {
    static let title = Font.system(size: 24, weight: .bold)
    static let headline = Font.system(size: 15, weight: .semibold)
    static let body = Font.system(size: 13)
    static let bodyStrong = Font.system(size: 13, weight: .semibold)
    static let caption = Font.system(size: 12)
    static let status = Font.system(size: 12, design: .monospaced)
    static let section = Font.system(size: 11, weight: .bold)
    static let label = Font.system(size: 10.5, weight: .semibold, design: .monospaced)
    static let counter = Font.system(size: 12.5, weight: .medium).monospacedDigit()
}

struct CardSectionTitle: View {
    let text: String

    var body: some View {
        Text(text)
            .font(TypeScale.section)
            .tracking(2.4)
            .foregroundStyle(WindowTheme.sectionText)
            .padding(.leading, Spacing.tight)
            .padding(.top, Spacing.large)
            .padding(.bottom, Spacing.small)
            .accessibilityAddTraits(.isHeader)
    }
}

struct OptionCard<Content: View>: View {
    @ViewBuilder let content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.medium) { content }
            .padding(Spacing.medium)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(RoundedRectangle(cornerRadius: 14, style: .continuous).fill(WindowTheme.card))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).strokeBorder(WindowTheme.hairline))
    }
}

struct CardStatusRow<Trailing: View>: View {
    let line: StatusLine
    @ViewBuilder var trailing: Trailing
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: Spacing.small) {
            Circle()
                .fill(WindowTheme.color(for: line.tone))
                .frame(width: 7, height: 7)
                .alignmentGuide(.firstTextBaseline) { $0[.bottom] - 1 }
            Text(line.text)
                .font(TypeScale.status)
                .monospacedDigit()
                .foregroundStyle(WindowTheme.secondaryText)
                .fixedSize(horizontal: false, vertical: true)
                .contentTransition(.opacity)
            Spacer(minLength: Spacing.small)
            trailing
        }
        .animation(WindowTheme.motion(reduceMotion), value: line)
        .accessibilityElement(children: .combine)
    }
}

extension CardStatusRow where Trailing == EmptyView {
    init(line: StatusLine) {
        self.init(line: line) { EmptyView() }
    }
}

struct ExplanationLine: View {
    let text: String
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        Text(text)
            .font(TypeScale.caption)
            .foregroundStyle(WindowTheme.secondaryText)
            .fixedSize(horizontal: false, vertical: true)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.leading, Spacing.tight)
            .contentTransition(.opacity)
            .animation(WindowTheme.motion(reduceMotion), value: text)
    }
}

struct KeyCap: View {
    let text: String

    var body: some View {
        Text(text)
            .font(.system(size: 11.5, weight: .semibold, design: .rounded))
            .foregroundStyle(WindowTheme.primaryText)
            .padding(.horizontal, 7)
            .padding(.vertical, 3)
            .background(RoundedRectangle(cornerRadius: 5, style: .continuous).fill(WindowTheme.well))
            .overlay(RoundedRectangle(cornerRadius: 5, style: .continuous).strokeBorder(Color.white.opacity(0.14)))
    }
}

struct LiveChip: View {
    private static let title = L10n.text("LIVE")
    private static let spokenTitle = L10n.text("Live")

    var body: some View {
        HStack(spacing: 5) {
            Circle().fill(WindowTheme.accent).frame(width: 5, height: 5)
            Text(Self.title).font(TypeScale.label).tracking(1.6).foregroundStyle(WindowTheme.accent)
        }
        .padding(.horizontal, 7)
        .padding(.vertical, 2)
        .background(Capsule().fill(WindowTheme.accent.opacity(0.12)))
        .accessibilityLabel(Self.spokenTitle)
    }
}
