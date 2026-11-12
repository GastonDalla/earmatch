using System.Globalization;
using System.Text.Json;

namespace Earmatch.Core;

/// The translations of one language, embedded as Localization/<language>.json:
/// { "strings": { "English key": "translation" }, "plurals": { "English key": { "one": "...", "other": "..." } } }.
/// Keys and printf specifiers are the macOS app's (Support/<language>.lproj); a plural entry holds the whole text per
/// form instead of a .stringsdict variable.
public sealed class LocalizationCatalog
{
    public static IReadOnlyList<string> Languages { get; } = ["en", "es"];

    private LocalizationCatalog(string language, CultureInfo culture, IReadOnlyDictionary<string, string> strings,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> plurals)
    {
        Language = language;
        Culture = culture;
        Strings = strings;
        Plurals = plurals;
    }

    public string Language { get; }

    /// The culture numbers are formatted in.
    public CultureInfo Culture { get; }

    public IReadOnlyDictionary<string, string> Strings { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Plurals { get; }

    /// The catalog for the user's display language: Spanish for any Spanish culture, English otherwise. Numbers follow
    /// that culture (es-AR keeps its own separators).
    public static LocalizationCatalog ForCulture(CultureInfo uiCulture)
    {
        var language = Languages.Contains(uiCulture.TwoLetterISOLanguageName) ? uiCulture.TwoLetterISOLanguageName : "en";
        var culture = language == uiCulture.TwoLetterISOLanguageName ? uiCulture : CultureInfo.GetCultureInfo(language);
        return Load(language, culture);
    }

    public static LocalizationCatalog Load(string language) => Load(language, CultureInfo.GetCultureInfo(language));

    public static LocalizationCatalog Load(string language, CultureInfo culture)
    {
        var name = $"Earmatch.Core.Localization.{language}.json";
        using var stream = typeof(LocalizationCatalog).Assembly.GetManifestResourceStream(name)
            ?? throw new ArgumentException($"No catalog for language '{language}'.", nameof(language));
        using var reader = new StreamReader(stream);
        return Parse(language, reader.ReadToEnd(), culture);
    }

    public static LocalizationCatalog Parse(string language, string json) => Parse(language, json, CultureInfo.GetCultureInfo(language));

    private static LocalizationCatalog Parse(string language, string json, CultureInfo culture)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var strings = new Dictionary<string, string>();
        if (root.TryGetProperty("strings", out var stringsJson))
        {
            foreach (var entry in stringsJson.EnumerateObject()) strings[entry.Name] = entry.Value.GetString() ?? "";
        }
        var plurals = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        if (root.TryGetProperty("plurals", out var pluralsJson))
        {
            foreach (var entry in pluralsJson.EnumerateObject())
            {
                plurals[entry.Name] = entry.Value.EnumerateObject().ToDictionary(form => form.Name, form => form.Value.GetString() ?? "");
            }
        }
        return new LocalizationCatalog(language, culture, strings, plurals);
    }

    public string Text(string key) => Strings.TryGetValue(key, out var text) ? text : key;

    /// The template of the plural form for the count (CLDR "one" for 1 in English and Spanish, "other" otherwise).
    public string? PluralTemplate(string key, int count)
    {
        if (!Plurals.TryGetValue(key, out var forms)) return null;
        var form = count == 1 ? "one" : "other";
        return forms.TryGetValue(form, out var template) ? template : forms.GetValueOrDefault("other");
    }
}
