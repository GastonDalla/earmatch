import Foundation

public enum OverlayActivation: Equatable, Sendable {
    case accessibilityMissing
    case screenRecordingMissing
    case rekordboxNotRunning
    case mainWindowNotFound
    case rekordboxNotFrontmost
    case wrongMode(RekordboxMode)
    case wrongLayout(String?)
    case decksNotFound
    case active

    public static func evaluate(isTrusted: Bool, isScreenRecordingGranted: Bool, isRunning: Bool,
                                layout: RekordboxLayout?, isFrontmost: Bool) -> OverlayActivation {
        guard isTrusted else { return .accessibilityMissing }
        guard isScreenRecordingGranted else { return .screenRecordingMissing }
        guard isRunning else { return .rekordboxNotRunning }
        guard let layout else { return .mainWindowNotFound }
        guard isFrontmost else { return .rekordboxNotFrontmost }
        guard layout.mode == .performance else { return .wrongMode(layout.mode) }
        guard layout.arrangement != nil else { return .wrongLayout(layout.layoutName) }
        guard layout.isSupportedPerformanceLayout else { return .decksNotFound }
        return .active
    }

    public var isActive: Bool { self == .active }

    /// Earmatch stays locked (no overlays, hotkeys or options) until both permissions are allowed.
    public var isPermissionMissing: Bool { self == .accessibilityMissing || self == .screenRecordingMissing }

    public var message: String {
        switch self {
        case .accessibilityMissing: L10n.text("Allow Earmatch in Privacy & Security › Accessibility")
        case .screenRecordingMissing: L10n.text("Allow Earmatch in Privacy & Security › Screen Recording")
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

/// Decides when the main window shows only its permissions screen.
///
/// It locks the moment a permission is missing and unlocks `unlockDelay` seconds after both are allowed, so the
/// second green check is seen before the options appear. Unlocking also waits for the monitor's activation, which
/// gates the overlays, the hotkeys and the menu bar menu, so the options never appear while the rest is still locked.
public struct PermissionLock: Equatable, Sendable {
    public static let unlockDelay: TimeInterval = 0.8

    public private(set) var isLocked: Bool
    private var allowedSince: TimeInterval?

    public init(arePermissionsMissing: Bool, activation: OverlayActivation) {
        isLocked = Self.isWaiting(arePermissionsMissing: arePermissionsMissing, activation: activation)
    }

    public static func isWaiting(arePermissionsMissing: Bool, activation: OverlayActivation) -> Bool {
        arePermissionsMissing || activation.isPermissionMissing
    }

    /// Records the permissions seen at `now` (a monotonic clock, in seconds). Returns the time at which to call
    /// again to finish unlocking, or nil when no unlock is pending.
    public mutating func update(arePermissionsMissing: Bool, activation: OverlayActivation, at now: TimeInterval) -> TimeInterval? {
        guard !Self.isWaiting(arePermissionsMissing: arePermissionsMissing, activation: activation) else {
            isLocked = true
            allowedSince = nil
            return nil
        }
        guard isLocked else { return nil }
        let since = allowedSince ?? now
        let unlocksAt = since + Self.unlockDelay
        guard now < unlocksAt else {
            isLocked = false
            allowedSince = nil
            return nil
        }
        allowedSince = since
        return unlocksAt
    }
}
