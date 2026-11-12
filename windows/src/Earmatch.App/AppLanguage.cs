using System.Globalization;
using Earmatch.Core;

namespace Earmatch.App;

/// Installs the catalog of the chosen language (AppLanguage.swift). System follows the Windows display language.
public static class AppLanguage
{
    /// The Windows display language, read before Earmatch changes the UI culture.
    private static readonly CultureInfo SystemCulture = CultureInfo.CurrentUICulture;

    public static AppLanguageChoice Choice { get; private set; } = AppLanguageChoice.System;

    public static void Install(AppLanguageChoice choice)
    {
        Choice = choice;
        var system = SystemCulture;
        var code = choice.LanguageCode(system.Name);
        // Numbers follow the user's own culture when it is in the chosen language (es-AR keeps its separators).
        var culture = system.TwoLetterISOLanguageName == code ? system : CultureInfo.GetCultureInfo(code);
        L10n.Install(LocalizationCatalog.ForCulture(culture));
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
