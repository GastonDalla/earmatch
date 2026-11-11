import Foundation

public enum MainWindowStatus: Equatable, Sendable {
    case live
    case off
    case waiting
    case needsPermission

    public init(arePermissionsMissing: Bool, isEnabled: Bool, activation: OverlayActivation) {
        if arePermissionsMissing {
            self = .needsPermission
        } else if !isEnabled {
            self = .off
        } else {
            self = activation.isActive ? .live : .waiting
        }
    }

    public var tone: StatusTone {
        switch self {
        case .live: .live
        case .off: .off
        case .waiting, .needsPermission: .waiting
        }
    }

    public var title: String {
        switch self {
        case .live: L10n.text("Live on rekordbox")
        case .off: L10n.text("Earmatch is off")
        case .waiting: L10n.text("Waiting for rekordbox")
        case .needsPermission: L10n.text("Needs permission")
        }
    }
}
