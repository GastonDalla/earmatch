import SwiftUI
import EarmatchCore

extension EnvironmentValues {
    @Entry var showsFocusRing = true
}

struct KeyboardControl<RingShape: InsettableShape>: ViewModifier {
    let shape: RingShape
    var activate: (() -> Void)?
    var step: ((Int) -> Void)?
    var hold: ((Bool) -> Void)?
    @FocusState private var isFocused: Bool
    @Environment(\.showsFocusRing) private var showsFocusRing

    func body(content: Content) -> some View {
        content
            .focusable()
            .focused($isFocused)
            .focusEffectDisabled()
            .onKeyPress(phases: .all, action: handle)
            .overlay {
                shape.inset(by: -3)
                    .strokeBorder(WindowTheme.accent, lineWidth: 2)
                    .opacity(isFocused && showsFocusRing ? 1 : 0)
                    .allowsHitTesting(false)
            }
            .onChange(of: isFocused) { _, focused in
                if !focused { hold?(false) }
            }
    }

    private func handle(_ press: KeyPress) -> KeyPress.Result {
        switch press.key {
        case .space, .return:
            if let hold {
                guard press.key == .space else { return .ignored }
                hold(press.phase != .up)
                return .handled
            }
            guard let activate else { return .ignored }
            if press.phase == .down { activate() }
            return .handled
        case .leftArrow, .rightArrow:
            guard let step else { return .ignored }
            if press.phase != .up { step(press.key == .leftArrow ? -1 : 1) }
            return .handled
        default:
            return .ignored
        }
    }
}

struct OptionSwitchRow: View {
    let title: String
    var subtitle: String?
    var hint: String?
    @Binding var isOn: Bool

    var body: some View {
        HStack(alignment: .center, spacing: Spacing.medium) {
            VStack(alignment: .leading, spacing: 2) {
                Text(title).font(TypeScale.headline).foregroundStyle(WindowTheme.primaryText)
                if let subtitle {
                    Text(subtitle)
                        .font(TypeScale.caption)
                        .foregroundStyle(WindowTheme.secondaryText)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            Spacer(minLength: Spacing.small)
            Toggle(title, isOn: $isOn)
                .toggleStyle(AccentSwitchStyle())
                .accessibilityHint(hint ?? subtitle ?? "")
        }
    }
}

struct AccentSwitchStyle: ToggleStyle {
    func makeBody(configuration: Configuration) -> some View {
        AccentSwitch(configuration: configuration)
    }
}

private struct AccentSwitch: View {
    let configuration: ToggleStyleConfiguration
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    private let shape = RoundedRectangle(cornerRadius: 12, style: .circular)

    var body: some View {
        let isOn = configuration.isOn
        shape
            .fill(isOn ? WindowTheme.accent : Color.white.opacity(0.16))
            .overlay { shape.strokeBorder(Color.white.opacity(isOn ? 0 : 0.08)) }
            .frame(width: 46, height: 26)
            .overlay(alignment: isOn ? .trailing : .leading) {
                Circle()
                    .fill(Color.white)
                    .shadow(color: .black.opacity(0.3), radius: 1.5, y: 1)
                    .padding(3)
            }
            .contentShape(shape)
            .onTapGesture(perform: toggle)
            .modifier(KeyboardControl(shape: shape, activate: toggle))
            .accessibilityRepresentation { Toggle(isOn: configuration.$isOn) { configuration.label } }
    }

    private func toggle() {
        withAnimation(reduceMotion ? nil : .snappy(duration: 0.18)) { configuration.isOn.toggle() }
    }
}

struct OptionPicker<Value: Hashable>: View {
    let title: String
    let options: [(label: String, value: Value)]
    let explanation: (Value) -> String
    @Binding var selection: Value
    @Namespace private var pill
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        SegmentRow(spacing: Spacing.tight) {
            ForEach(options, id: \.value) { option in
                let isSelected = option.value == selection
                Text(option.label)
                    .font(TypeScale.bodyStrong)
                    .foregroundStyle(isSelected ? WindowTheme.onAccent : WindowTheme.secondaryText)
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
                    .padding(.horizontal, 6)
                    .frame(maxWidth: .infinity)
                    .frame(height: 30)
                    .background {
                        if isSelected {
                            RoundedRectangle(cornerRadius: 7, style: .continuous)
                                .fill(WindowTheme.accent)
                                .matchedGeometryEffect(id: "pill", in: pill)
                        }
                    }
                    .contentShape(Rectangle())
                    .onTapGesture { select(option.value) }
                    .help(explanation(option.value))
            }
        }
        .padding(Spacing.tight)
        .surface(cornerRadius: 10, fill: WindowTheme.well, stroke: WindowTheme.hairline)
        .modifier(KeyboardControl(shape: RoundedRectangle(cornerRadius: 10, style: .continuous), step: step))
        .accessibilityRepresentation {
            Picker(title, selection: $selection) {
                ForEach(options, id: \.value) { Text($0.label).tag($0.value) }
            }
            .pickerStyle(.segmented)
        }
        .accessibilityHint(explanation(selection))
    }

    private func select(_ value: Value) {
        guard value != selection else { return }
        withAnimation(reduceMotion ? nil : .snappy(duration: 0.22)) { selection = value }
    }

    private func step(_ delta: Int) {
        guard let index = options.firstIndex(where: { $0.value == selection }) else { return }
        let next = min(max(index + delta, 0), options.count - 1)
        select(options[next].value)
    }
}

private struct SegmentRow: Layout {
    let spacing: CGFloat

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let sizes = subviews.map { $0.sizeThatFits(.unspecified) }
        let idealWidth = sizes.map(\.width).reduce(0, +) + gaps(subviews)
        return CGSize(width: proposal.width ?? idealWidth, height: sizes.map(\.height).max() ?? 0)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        let ideal = subviews.map { Double($0.sizeThatFits(.unspecified).width) }
        let widths = SegmentWidths.distribute(ideal: ideal, available: Double(bounds.width - gaps(subviews)))
        var x = bounds.minX
        for (subview, width) in zip(subviews, widths) {
            subview.place(at: CGPoint(x: x, y: bounds.midY), anchor: .leading,
                          proposal: ProposedViewSize(width: CGFloat(width), height: bounds.height))
            x += CGFloat(width) + spacing
        }
    }

    private func gaps(_ subviews: Subviews) -> CGFloat {
        spacing * CGFloat(max(subviews.count - 1, 0))
    }
}

struct ActionButton<Label: View>: View {
    enum Kind {
        case accent
        case pill
        case link
    }

    let kind: Kind
    let accessibilityText: String
    let action: () -> Void
    @ViewBuilder let label: Label
    @State private var isPressed = false
    @State private var isHovered = false
    @State private var size = CGSize.zero

    var body: some View {
        styled
            .contentShape(Rectangle())
            .background { GeometryReader { proxy in Color.clear.onAppear { size = proxy.size }.onChange(of: proxy.size) { _, new in size = new } } }
            .gesture(
                DragGesture(minimumDistance: 0)
                    .onChanged { value in isPressed = CGRect(origin: .zero, size: size).contains(value.location) }
                    .onEnded { value in
                        isPressed = false
                        if CGRect(origin: .zero, size: size).contains(value.location) { action() }
                    }
            )
            .onHover { isHovered = $0 }
            .modifier(KeyboardControl(shape: ringShape, activate: action))
            .help(accessibilityText)
            .accessibilityElement(children: .ignore)
            .accessibilityLabel(accessibilityText)
            .accessibilityAddTraits(kind == .link ? .isLink : .isButton)
            .accessibilityAction(.default, action)
    }

    private var ringShape: RoundedRectangle {
        RoundedRectangle(cornerRadius: kind == .pill ? 14 : 7, style: .continuous)
    }

    @ViewBuilder private var styled: some View {
        switch kind {
        case .accent:
            label
                .font(TypeScale.bodyStrong)
                .foregroundStyle(WindowTheme.onAccent)
                .padding(.horizontal, 12)
                .padding(.vertical, 5)
                .background(WindowTheme.accent, in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                .opacity(isPressed ? 0.75 : 1)
        case .pill:
            label
                .font(TypeScale.bodyStrong)
                .foregroundStyle(WindowTheme.primaryText.opacity(isHovered ? 0.95 : 0.8))
                .padding(.horizontal, Spacing.medium)
                .padding(.vertical, 7)
                .surface(cornerRadius: 12, fill: isHovered ? Color.white.opacity(0.07) : WindowTheme.card, stroke: WindowTheme.hairline)
                .opacity(isPressed ? 0.7 : 1)
        case .link:
            label
                .underline(isHovered, color: WindowTheme.accent.opacity(0.6))
                .padding(.horizontal, Spacing.tight)
                .padding(.vertical, 2)
                .opacity(isPressed ? 0.7 : 1)
        }
    }
}

struct PreviewPeekControl: View {
    let isLive: Bool
    @Binding var isPeeking: Bool
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        HStack(spacing: Spacing.small) {
            if isPeeking {
                Text(PreviewText.unchanged)
                    .font(TypeScale.label)
                    .foregroundStyle(WindowTheme.secondaryText)
                    .lineLimit(1)
                    .transition(.opacity)
            }
            ZStack(alignment: .trailing) {
                Text(PreviewText.title)
                    .font(TypeScale.label)
                    .tracking(2.2)
                    .foregroundStyle(WindowTheme.sectionText)
                    .opacity(isLive ? 0 : 1)
                LiveChip().opacity(isLive ? 1 : 0)
            }
            .animation(WindowTheme.motion(reduceMotion), value: isLive)
            Image(systemName: isPeeking ? "eye.slash" : "eye")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(isPeeking ? WindowTheme.accent : WindowTheme.secondaryText)
                .frame(width: 24, height: 18)
                .contentShape(Rectangle())
                .gesture(
                    DragGesture(minimumDistance: 0)
                        .onChanged { _ in if !isPeeking { isPeeking = true } }
                        .onEnded { _ in isPeeking = false }
                )
                .modifier(KeyboardControl(shape: RoundedRectangle(cornerRadius: 5, style: .continuous),
                                          hold: { isPeeking = $0 }))
                .help(PreviewText.peekHelp)
                .accessibilityElement(children: .ignore)
                .accessibilityLabel(isPeeking ? PreviewText.peeking : PreviewText.peek)
                .accessibilityAddTraits(.isButton)
                .accessibilityAction { isPeeking.toggle() }
        }
        .animation(WindowTheme.motion(reduceMotion, duration: 0.15), value: isPeeking)
    }
}

struct PreviewScreen<Content: View>: View {
    let height: CGFloat
    @ViewBuilder var content: Content

    var body: some View {
        content
            .frame(height: height)
            .frame(maxWidth: .infinity)
            .background(Color.black)
            .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
            .overlay { RoundedRectangle(cornerRadius: 8, style: .continuous).strokeBorder(WindowTheme.hairline) }
    }
}

private enum PreviewText {
    static var title: String { L10n.text("PREVIEW") }
    static var peekHelp: String { L10n.text("Hold to see rekordbox unchanged, as holding Right Option does") }
    static var peeking: String { L10n.text("Showing rekordbox unchanged") }
    static var peek: String { L10n.text("Peek at rekordbox unchanged") }
    static var unchanged: String { L10n.text("rekordbox unchanged") }
}
