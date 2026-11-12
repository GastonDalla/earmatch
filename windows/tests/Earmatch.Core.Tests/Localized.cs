using System.Globalization;
using Earmatch.Core;

namespace Earmatch.Core.Tests;

/// Runs code under a marker lookup ("⟦key⟧") or a Spanish culture, the way the Swift tests override L10n.
static class Localized
{
    public static readonly L10n.Lookup Marker = key => $"⟦{key}⟧";

    public static T Marked<T>(Func<T> body) => L10n.WithOverride(Marker, body);

    public static T InSpanishCulture<T>(Func<T> body)
    {
        var spanish = CultureInfo.GetCultureInfo("es-ES");
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = spanish;
        try
        {
            return L10n.WithOverride(Marker, body, spanish);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// The text with every marked part removed: empty or punctuation only when everything went through the lookup.
    public static string Unmarked(string text)
    {
        var depth = 0;
        var rest = new System.Text.StringBuilder();
        foreach (var character in text)
        {
            switch (character)
            {
                case '⟦': depth++; break;
                case '⟧': depth--; break;
                default: if (depth == 0) rest.Append(character); break;
            }
        }
        return rest.ToString();
    }

    public static bool IsLocalized(string text) => text.Length > 0 && !Unmarked(text).Any(char.IsLetter);
}
