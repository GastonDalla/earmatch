import Foundation

public enum MainWindowPane: String, CaseIterable, Sendable {
    case live
    case overlay
    case practice
    case setup

    public static let storageKey = "mainWindowPane"

    public var title: String {
        switch self {
        case .live: L10n.text("Live")
        case .overlay: L10n.text("Overlay")
        case .practice: L10n.text("Practice")
        case .setup: L10n.text("Setup")
        }
    }

    public var shortcutNumber: Int { index + 1 }

    public var keyEquivalent: String { String(shortcutNumber) }

    public init?(keyEquivalent: String) {
        guard let number = Int(keyEquivalent), keyEquivalent.count == 1,
              Self.allCases.indices.contains(number - 1)
        else { return nil }
        self = Self.allCases[number - 1]
    }

    public func moved(by offset: Int) -> MainWindowPane {
        let target = min(max(index + offset, 0), Self.allCases.count - 1)
        return Self.allCases[target]
    }

    public static func initial(stored: String?, arePermissionsMissing: Bool) -> MainWindowPane {
        if arePermissionsMissing { return .setup }
        return stored.flatMap(Self.init(rawValue:)) ?? .live
    }

    private var index: Int { Self.allCases.firstIndex(of: self) ?? 0 }
}
