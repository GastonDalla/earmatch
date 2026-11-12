using System.Globalization;

namespace Earmatch.Core;

public static class L10n
{
    public delegate string Lookup(string key);

    public delegate string? PluralLookup(string key, int count);

    private sealed record Source(Lookup Text, PluralLookup? Plural, CultureInfo? Culture);

    private static readonly AsyncLocal<Source?> Override = new();
    private static volatile Source _installed = new(key => key, null, null);

    public static void Install(Lookup lookup, CultureInfo culture, PluralLookup? plural = null) =>
        _installed = new Source(lookup, plural, culture);

    public static void Install(LocalizationCatalog catalog) => Install(catalog.Text, catalog.Culture, catalog.PluralTemplate);

    public static CultureInfo? Culture => Current.Culture;

    public static T WithOverride<T>(Lookup lookup, Func<T> body, CultureInfo? culture = null, PluralLookup? plural = null)
    {
        var previous = Override.Value;
        Override.Value = new Source(lookup, plural, culture);
        try
        {
            return body();
        }
        finally
        {
            Override.Value = previous;
        }
    }

    public static T WithOverride<T>(LocalizationCatalog catalog, Func<T> body) =>
        WithOverride(catalog.Text, body, catalog.Culture, catalog.PluralTemplate);

    public static T English<T>(Func<T> body) => WithOverride(key => key, body);

    public static string Text(string key) => Current.Text(key);

    public static string Template(string key) => Text(key);

    public static string Format(string key, params object?[] arguments) =>
        Printf.Format(Text(key), Culture ?? CultureInfo.InvariantCulture, arguments);

    public static string Plural(string key, int count)
    {
        var source = Current;
        var template = source.Plural?.Invoke(key, count) ?? source.Text(key);
        return Printf.Format(template, source.Culture ?? CultureInfo.InvariantCulture, [count]);
    }

    private static Source Current => Override.Value ?? _installed;
}
