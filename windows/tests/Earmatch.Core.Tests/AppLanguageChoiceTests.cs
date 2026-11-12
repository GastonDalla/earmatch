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
}
