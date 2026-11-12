using System.Globalization;
using Earmatch.Core;

namespace Earmatch.App;

public static class AppLanguage
{
    private static readonly CultureInfo RegionalCulture = CultureInfo.CurrentCulture;

    public static AppLanguageChoice Choice { get; private set; } = AppLanguageChoice.System;

    public static void Install(AppLanguageChoice choice)
    {
        Choice = choice;
        var preferred = SystemInfo.PreferredLanguages;
        var code = choice.LanguageCodeFrom(preferred);
        L10n.Install(LocalizationCatalog.Load(code, AppLanguageChoice.FormatCulture(code, RegionalCulture)));
        var uiCulture = UiCulture(code, preferred);
        CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
        CultureInfo.CurrentUICulture = uiCulture;
    }

    private static CultureInfo UiCulture(string code, IReadOnlyList<string> preferred)
    {
        foreach (var name in preferred)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(name);
                if (culture.TwoLetterISOLanguageName == code) return culture;
            }
            catch (CultureNotFoundException)
            {
            }
        }
        return CultureInfo.GetCultureInfo(code);
    }
}
