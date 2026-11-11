import Foundation
import Testing
import EarmatchCore

@Suite struct AppLanguageChoiceTests {
    @Test func theSelectorOffersSystemEnglishAndSpanishInThatOrder() {
        #expect(AppLanguageChoice.allCases == [.system, .english, .spanish])
    }

    @Test func eachChoiceIsStoredUnderAStableValue() {
        #expect(AppLanguageChoice.allCases.map(\.rawValue) == ["system", "en", "es"])
        for choice in AppLanguageChoice.allCases {
            #expect(AppLanguageChoice(storedValue: choice.rawValue) == choice)
        }
    }

    @Test(arguments: [nil, "", "fr", "English", "ES"])
    func anUnknownStoredValueFollowsTheSystem(stored: String?) {
        #expect(AppLanguageChoice(storedValue: stored) == .system)
    }

    @Test func anExplicitChoiceWinsOverTheSystemLanguage() {
        #expect(AppLanguageChoice.english.languageCode(systemPreferred: "es") == "en")
        #expect(AppLanguageChoice.spanish.languageCode(systemPreferred: "en") == "es")
        #expect(AppLanguageChoice.spanish.languageCode(systemPreferred: nil) == "es")
    }

    @Test(arguments: [("es", "es"), ("en", "en"), ("es-419", "es"), ("en_GB", "en"), ("fr", "en"), (nil, "en")] as [(String?, String)])
    func theSystemChoiceFollowsTheFirstLanguageEarmatchHas(preferred: String?, expected: String) {
        #expect(AppLanguageChoice.system.languageCode(systemPreferred: preferred) == expected)
    }

    @Test func languagesAreNamedInTheirOwnLanguageAndSystemIsTranslated() {
        let labels = L10n.$override.withValue({ "⟦\($0)⟧" }) { AppLanguageChoice.allCases.map(\.label) }
        #expect(labels == ["⟦System⟧", "⟦English⟧", "⟦Español⟧"])
    }

    @Test func everyChoiceExplainsWhatItDoes() {
        let explanations = L10n.english { AppLanguageChoice.allCases.map(\.explanation) }
        #expect(explanations.allSatisfy { !$0.isEmpty })
        #expect(explanations[0] != explanations[1])
    }
}
