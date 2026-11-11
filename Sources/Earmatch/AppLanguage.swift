import Foundation
import EarmatchCore

enum AppLanguage {
    private(set) static var choice = AppLanguageChoice.system
    private(set) static var code = AppLanguageChoice.fallbackCode
    private(set) static var locale = Locale.current

    static var storedChoice: AppLanguageChoice {
        AppLanguageChoice(storedValue: UserDefaults.standard.string(forKey: AppLanguageChoice.storageKey))
    }

    static func locale(for code: String, current: Locale) -> Locale {
        let language = Locale.Language(identifier: code)
        if current.language.languageCode == language.languageCode { return current }
        var components = Locale.Components(languageCode: language.languageCode, script: language.script, languageRegion: language.region)
        if language.region == nil { components.region = current.region }
        return Locale(components: components)
    }

    static func install(_ newChoice: AppLanguageChoice) {
        choice = newChoice
        code = newChoice.languageCode(systemPreferred: Bundle.main.preferredLocalizations.first)
        locale = locale(for: code, current: .current)
        let bundle = Bundle.main.path(forResource: code, ofType: "lproj").flatMap(Bundle.init(path:)) ?? Bundle.main
        L10n.install({ bundle.localizedString(forKey: $0, value: $0, table: nil) }, locale: locale)
    }

    static func select(_ newChoice: AppLanguageChoice) {
        UserDefaults.standard.set(newChoice.rawValue, forKey: AppLanguageChoice.storageKey)
        install(newChoice)
    }
}
