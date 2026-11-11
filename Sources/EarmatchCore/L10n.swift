import Foundation
import os

public enum L10n {
    public typealias Lookup = @Sendable (String) -> String

    @TaskLocal public static var override: Lookup?
    @TaskLocal public static var overrideLocale: Locale?

    private struct Installation: Sendable {
        var lookup: Lookup = { $0 }
        var locale: Locale?
    }

    private static let installation = OSAllocatedUnfairLock(initialState: Installation())

    public static func install(_ lookup: @escaping Lookup, locale: Locale) {
        installation.withLock { $0 = Installation(lookup: lookup, locale: locale) }
    }

    public static var locale: Locale? { override == nil ? installation.withLock { $0.locale } : overrideLocale }

    public static func english<T>(_ body: () -> T) -> T {
        $override.withValue({ $0 }) { $overrideLocale.withValue(nil, operation: body) }
    }

    public static func text(_ key: String) -> String {
        (override ?? installation.withLock { $0.lookup })(key)
    }

    public static func template(_ key: String) -> String {
        text(key)
    }

    public static func format(_ key: String, _ arguments: any CVarArg...) -> String {
        String(format: text(key), locale: locale, arguments: arguments)
    }

    public static func plural(_ key: String, _ count: Int) -> String {
        String(format: text(key), locale: locale ?? Locale(identifier: "en"), arguments: [count])
    }
}
