import Foundation

public enum L10n {
    public typealias Lookup = @Sendable (String) -> String

    @TaskLocal public static var override: Lookup?
    @TaskLocal public static var overrideLocale: Locale?

    nonisolated(unsafe) private static var installed: Lookup = { $0 }
    nonisolated(unsafe) private static var installedLocale: Locale?

    public static func install(_ lookup: @escaping Lookup, locale: Locale) {
        installed = lookup
        installedLocale = locale
    }

    public static var locale: Locale? { override == nil ? installedLocale : overrideLocale }

    public static func english<T>(_ body: () -> T) -> T {
        $override.withValue({ $0 }) { $overrideLocale.withValue(nil, operation: body) }
    }

    public static func text(_ key: String) -> String {
        (override ?? installed)(key)
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
