import Foundation

public enum AppLanguageChoice: String, CaseIterable, Sendable {
    case system
    case english = "en"
    case spanish = "es"

    public static let storageKey = "appLanguage"
    public static let fallbackCode = "en"

    public init(storedValue: String?) {
        self = storedValue.flatMap(Self.init(rawValue:)) ?? .system
    }

    public func languageCode(systemPreferred: String?) -> String {
        switch self {
        case .english, .spanish:
            return rawValue
        case .system:
            let language = systemPreferred?.split(whereSeparator: { $0 == "-" || $0 == "_" }).first.map(String.init)
            guard let language, let choice = Self(rawValue: language), choice != .system else { return Self.fallbackCode }
            return choice.rawValue
        }
    }

    public var label: String {
        switch self {
        case .system: L10n.text("System")
        case .english: L10n.text("English")
        case .spanish: L10n.text("Español")
        }
    }

    public var explanation: String {
        switch self {
        case .system: L10n.text("Follows the language order in System Settings")
        case .english, .spanish: L10n.text("The window and the menus switch right away")
        }
    }
}
