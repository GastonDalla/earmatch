using System.Globalization;
using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class L10nTests
{
    const string PluralCatalog = """
        {
          "strings": { "Earmatch is off": "Earmatch está apagado", "Live · %.0f fps · %.1f ms": "En vivo · %.0f fps · %.1f ms" },
          "plurals": { "%ld mixes saved": { "one": "%ld mezcla guardada", "other": "%ld mezclas guardadas" } }
        }
        """;

    [Fact]
    public void WithoutTranslationsTheEnglishKeyIsTheText()
    {
        Assert.Equal("Earmatch is off", L10n.Text("Earmatch is off"));
        Assert.Equal("Live · 112 fps", L10n.Format("Live · %.0f fps", 112.4));
    }

    [Fact]
    public void NumbersInFormatsAreNotTranslated()
    {
        var text = Localized.Marked(() => L10n.Format("Live · %.0f fps · %.1f ms", 112.0, 2.14));
        Assert.Equal("⟦Live · 112 fps · 2.1 ms⟧", text);
    }

    [Fact]
    public void FormatsReadObjectsIntegersAndPositions()
    {
        Assert.Equal("rekordbox 7.2.19 connected · 2Deck Vertical", L10n.Format("rekordbox %@ connected · %@", "7.2.19", "2Deck Vertical"));
        Assert.Equal("Deck 2 → Deck 1", L10n.Format("Deck %ld → Deck %ld", 2, 1));
        Assert.Equal("1 of 2: 2 1", L10n.Format("%ld of %ld: %2$@ %1$@", 1, 2));
        Assert.Equal("100% sure, 07", L10n.Format("%ld%% sure, %02d", 100, 7));
    }

    [Fact]
    public void ASpanishCatalogTranslatesAndFormatsNumbersTheSpanishWay()
    {
        var catalog = LocalizationCatalog.Parse("es", PluralCatalog);

        var (text, format) = L10n.WithOverride(catalog, () => (L10n.Text("Earmatch is off"), L10n.Format("Live · %.0f fps · %.1f ms", 112.0, 2.14)));

        Assert.Equal("Earmatch está apagado", text);
        Assert.Equal("En vivo · 112 fps · 2,1 ms", format);
        Assert.Equal("es", catalog.Culture.TwoLetterISOLanguageName);
    }

    [Fact]
    public void AKeyMissingFromTheCatalogShowsTheEnglishKey()
    {
        var catalog = LocalizationCatalog.Parse("es", PluralCatalog);

        Assert.Equal("Waiting for rekordbox 7", L10n.WithOverride(catalog, () => L10n.Text("Waiting for rekordbox 7")));
    }

    [Fact]
    public void PluralsPickTheFormForTheCount()
    {
        var catalog = LocalizationCatalog.Parse("es", PluralCatalog);

        var forms = L10n.WithOverride(catalog, () => new[] { 0, 1, 2 }.Select(count => L10n.Plural("%ld mixes saved", count)).ToList());

        Assert.Equal(["0 mezclas guardadas", "1 mezcla guardada", "2 mezclas guardadas"], forms);
        Assert.Equal("3 mixes saved", L10n.Plural("%ld mixes saved", 3));
        Assert.Equal("⟦1 mixes saved⟧", Localized.Marked(() => L10n.Plural("%ld mixes saved", 1)));
    }

    [Fact]
    public void EnglishRunsCodeWithoutTheInstalledTranslations()
    {
        var catalog = LocalizationCatalog.Parse("es", PluralCatalog);

        var text = L10n.WithOverride(catalog, () => L10n.English(() => L10n.Format("Live · %.0f fps · %.1f ms", 112.0, 2.14)));

        Assert.Equal("Live · 112 fps · 2.1 ms", text);
    }

    [Fact]
    public void TheCatalogFollowsTheWindowsDisplayLanguage()
    {
        Assert.Equal("es", LocalizationCatalog.ForCulture(CultureInfo.GetCultureInfo("es-AR")).Language);
        Assert.Equal("en", LocalizationCatalog.ForCulture(CultureInfo.GetCultureInfo("en-GB")).Language);
        Assert.Equal("en", LocalizationCatalog.ForCulture(CultureInfo.GetCultureInfo("ja-JP")).Language);
        Assert.Equal("es-AR", LocalizationCatalog.ForCulture(CultureInfo.GetCultureInfo("es-AR")).Culture.Name);
    }
}
