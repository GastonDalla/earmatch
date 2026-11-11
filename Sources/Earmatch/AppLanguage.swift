import Foundation
import EarmatchCore

enum AppLanguage {
    static let code = Bundle.main.preferredLocalizations.first ?? "en"

    static let locale = locale(for: code, current: .current)

    static func locale(for code: String, current: Locale) -> Locale {
        let language = Locale.Language(identifier: code)
        if current.language.languageCode == language.languageCode { return current }
        var components = Locale.Components(languageCode: language.languageCode, script: language.script, languageRegion: language.region)
        if language.region == nil { components.region = current.region }
        return Locale(components: components)
    }

    static func install() {
        L10n.install({ Bundle.main.localizedString(forKey: $0, value: $0, table: nil) }, locale: locale)
    }
}
