using System.Globalization;

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
        public string LanguageCode(string? systemPreferred) => choice.LanguageCodeFrom([systemPreferred]);

        public string LanguageCodeFrom(IEnumerable<string?> preferredLanguages)
        {
            if (choice != AppLanguageChoice.System) return choice.RawValue;
            foreach (var preferred in preferredLanguages)
            {
                if (AppLanguageChoice.FromStoredValue(preferred?.Split('-', '_')[0]) is var match && match != AppLanguageChoice.System)
                    return match.RawValue;
            }
            return AppLanguageChoice.FallbackCode;
        }

        /// English and Español are named in their own language; System is translated.
        public string Label => choice switch
        {
            AppLanguageChoice.English => L10n.Text("English"),
            AppLanguageChoice.Spanish => L10n.Text("Español"),
            _ => L10n.Text("System"),
        };

        public string Explanation => choice == AppLanguageChoice.System
            ? L10n.Text("Follows the language order in Windows Settings")
            : L10n.Text("The window and the menus switch right away");
    }

    extension(AppLanguageChoice)
    {
        public static CultureInfo FormatCulture(string languageCode, CultureInfo regional)
        {
            if (string.Equals(regional.TwoLetterISOLanguageName, languageCode, StringComparison.OrdinalIgnoreCase)) return regional;
            var culture = (CultureInfo)CultureInfo.GetCultureInfo(languageCode).Clone();
            culture.NumberFormat = (NumberFormatInfo)regional.NumberFormat.Clone();
            var dates = culture.DateTimeFormat;
            var regionalDates = regional.DateTimeFormat;
            dates.ShortTimePattern = regionalDates.ShortTimePattern;
            dates.LongTimePattern = regionalDates.LongTimePattern;
            dates.FirstDayOfWeek = regionalDates.FirstDayOfWeek;
            return culture;
        }

        public static IReadOnlyList<AppLanguageChoice> AllCases => Enum.GetValues<AppLanguageChoice>();

        public static string StorageKey => "appLanguage";

        public static string FallbackCode => "en";

        public static AppLanguageChoice FromStoredValue(string? stored) =>
            Enum.GetValues<AppLanguageChoice>().FirstOrDefault(choice => choice.RawValue == stored, AppLanguageChoice.System);
    }
}
