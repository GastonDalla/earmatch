public enum OverlayActivation: Equatable, Sendable {
    case accessibilityMissing
    case rekordboxNotRunning
    case mainWindowNotFound
    case rekordboxNotFrontmost
    case wrongMode(RekordboxMode)
    case wrongLayout(String?)
    case decksNotFound
    case active

    public static func evaluate(isTrusted: Bool, isRunning: Bool, layout: RekordboxLayout?, isFrontmost: Bool) -> OverlayActivation {
        guard isTrusted else { return .accessibilityMissing }
        guard isRunning else { return .rekordboxNotRunning }
        guard let layout else { return .mainWindowNotFound }
        guard isFrontmost else { return .rekordboxNotFrontmost }
        guard layout.mode == .performance else { return .wrongMode(layout.mode) }
        guard layout.arrangement != nil else { return .wrongLayout(layout.layoutName) }
        guard layout.isSupportedPerformanceLayout else { return .decksNotFound }
        return .active
    }

    public var isActive: Bool { self == .active }

    public var message: String {
        switch self {
        case .accessibilityMissing: L10n.text("Allow Earmatch in Privacy & Security › Accessibility")
        case .rekordboxNotRunning: L10n.text("Waiting for rekordbox 7")
        case .mainWindowNotFound: L10n.text("Waiting for the rekordbox window")
        case .rekordboxNotFrontmost: L10n.text("Paused until rekordbox is in front")
        case .wrongMode: L10n.text("Switch rekordbox to PERFORMANCE mode")
        case .wrongLayout("Browse"): L10n.text("Browse view is open; switch back to a 2Deck or 4Deck layout")
        case .wrongLayout: L10n.text("Switch the rekordbox layout to a 2Deck or 4Deck layout")
        case .decksNotFound: L10n.text("Waiting for the decks and the waveform")
        case .active: L10n.text("Active on rekordbox")
        }
    }
}
