using System.Globalization;
using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class AppLanguageChoiceTests
{
    [Fact]
    public void TheSelectorOffersSystemEnglishAndSpanishInThatOrder()
    {
        Assert.Equal([AppLanguageChoice.System, AppLanguageChoice.English, AppLanguageChoice.Spanish], AppLanguageChoice.AllCases);
    }

    [Fact]
    public void EachChoiceIsStoredUnderAStableValue()
    {
        Assert.Equal(["system", "en", "es"], AppLanguageChoice.AllCases.Select(choice => choice.RawValue));
        foreach (var choice in AppLanguageChoice.AllCases)
        {
            Assert.Equal(choice, AppLanguageChoice.FromStoredValue(choice.RawValue));
        }
        Assert.Equal("appLanguage", AppLanguageChoice.StorageKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("English")]
    [InlineData("ES")]
    public void AnUnknownStoredValueFollowsTheSystem(string? stored)
    {
        Assert.Equal(AppLanguageChoice.System, AppLanguageChoice.FromStoredValue(stored));
    }

    [Fact]
    public void AnExplicitChoiceWinsOverTheSystemLanguage()
    {
        Assert.Equal("en", AppLanguageChoice.English.LanguageCode("es"));
        Assert.Equal("es", AppLanguageChoice.Spanish.LanguageCode("en"));
        Assert.Equal("es", AppLanguageChoice.Spanish.LanguageCode(null));
    }

    [Theory]
    [InlineData("es", "es")]
    [InlineData("en", "en")]
    [InlineData("es-419", "es")]
    [InlineData("en_GB", "en")]
    [InlineData("es-AR", "es")]
    [InlineData("fr", "en")]
    [InlineData(null, "en")]
    public void TheSystemChoiceFollowsTheFirstLanguageEarmatchHas(string? preferred, string expected)
    {
        Assert.Equal(expected, AppLanguageChoice.System.LanguageCode(preferred));
    }

    [Fact]
    public void LanguagesAreNamedInTheirOwnLanguageAndSystemIsTranslated()
    {
        var labels = Localized.Marked(() => AppLanguageChoice.AllCases.Select(choice => choice.Label).ToList());
        Assert.Equal(["⟦System⟧", "⟦English⟧", "⟦Español⟧"], labels);
    }

    [Fact]
    public void EveryChoiceExplainsWhatItDoes()
    {
        var explanations = L10n.English(() => AppLanguageChoice.AllCases.Select(choice => choice.Explanation).ToList());
        Assert.All(explanations, explanation => Assert.NotEmpty(explanation));
        Assert.NotEqual(explanations[0], explanations[1]);
        Assert.Contains("Windows", explanations[0]);
    }

    [Theory]
    [InlineData(new[] { "ca-ES", "es-ES", "en-US" }, "es")]
    [InlineData(new[] { "en-GB", "es-AR" }, "en")]
    [InlineData(new[] { "fr-FR", "de-DE" }, "en")]
    [InlineData(new string[0], "en")]
    public void TheSystemChoiceTakesTheFirstLanguageOfTheListThatEarmatchHas(string[] preferred, string expected)
    {
        Assert.Equal(expected, AppLanguageChoice.System.LanguageCodeFrom(preferred));
    }

    [Fact]
    public void AnExplicitChoiceIgnoresThePreferredList()
    {
        Assert.Equal("en", AppLanguageChoice.English.LanguageCodeFrom(["es-AR"]));
    }

    [Fact]
    public void FormatCulture_KeepsTheRegionalFormatWhenItIsInTheChosenLanguage()
    {
        var regional = CultureInfo.GetCultureInfo("es-AR");
        Assert.Same(regional, AppLanguageChoice.FormatCulture("es", regional));
    }

    [Fact]
    public void FormatCulture_FormatsNumbersAndTimesTheRegionalWayInAnotherLanguage()
    {
        var regional = CultureInfo.GetCultureInfo("de-DE");
        var culture = AppLanguageChoice.FormatCulture("en", regional);

        Assert.Equal("en", culture.TwoLetterISOLanguageName);
        Assert.Equal("1.234,5", 1234.5.ToString("#,0.0", culture));
        Assert.Equal("15:05", new DateTime(2026, 11, 12, 15, 5, 0).ToString(culture.DateTimeFormat.ShortTimePattern, culture));
        Assert.Equal("Nov", culture.DateTimeFormat.GetAbbreviatedMonthName(11));
    }
}
