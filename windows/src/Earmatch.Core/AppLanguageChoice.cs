namespace Earmatch.Core;

/// The language the window and the menus use: the Windows display language, or English or Spanish whatever it is.
public enum AppLanguageChoice
{
    System,
    English,
    Spanish,
}

public static class AppLanguageChoiceMembers
{
    extension(AppLanguageChoice choice)
    {
        /// The stored value, the macOS raw value.
        public string RawValue => choice switch
        {
            AppLanguageChoice.English => "en",
            AppLanguageChoice.Spanish => "es",
            _ => "system",
        };

        /// The catalog language for this choice; System follows the first preferred language Earmatch has.
        public string LanguageCode(string? systemPreferred)
        {
            if (choice != AppLanguageChoice.System) return choice.RawValue;
            var language = systemPreferred?.Split('-', '_')[0];
            return AppLanguageChoice.FromStoredValue(language) is var match && match != AppLanguageChoice.System
                ? match.RawValue
                : AppLanguageChoice.FallbackCode;
        }

        /// English and Español are named in their own language; System is translated.
        public string Label => choice switch
        {
            AppLanguageChoice.English => L10n.Text("English"),
            AppLanguageChoice.Spanish => L10n.Text("Español"),
            _ => L10n.Text("System"),
        };

        public string Explanation => choice == AppLanguageChoice.System
            ? L10n.Text("Follows the Windows display language")
            : L10n.Text("The window and the menus switch right away");
    }

    extension(AppLanguageChoice)
    {
        public static IReadOnlyList<AppLanguageChoice> AllCases => Enum.GetValues<AppLanguageChoice>();

        public static string StorageKey => "appLanguage";

        public static string FallbackCode => "en";

        public static AppLanguageChoice FromStoredValue(string? stored) =>
            Enum.GetValues<AppLanguageChoice>().FirstOrDefault(choice => choice.RawValue == stored, AppLanguageChoice.System);
    }
}
