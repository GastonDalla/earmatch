using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class PracticeTextTests
{
    static T Spanish<T>(Func<T> body) => L10n.WithOverride(LocalizationCatalog.Load("es"), body);
    static T English<T>(Func<T> body) => L10n.WithOverride(LocalizationCatalog.Load("en"), body);

    [Fact]
    public void MillisecondsAreRoundedAndSignedOnlyWhenAsked()
    {
        Assert.Equal("6 ms", PracticeText.Milliseconds(6.4));
        Assert.Equal("+6 ms", PracticeText.Milliseconds(6.4, signed: true));
        Assert.Equal("-15 ms", PracticeText.Milliseconds(-14.5, signed: true));
        Assert.Equal("0 ms", PracticeText.Milliseconds(-0.2, signed: true));
        Assert.Equal("1,250 ms", English(() => PracticeText.Milliseconds(1250)));
    }

    [Fact]
    public void TempoDifferencesShowTwoDecimalsInTheUsersNumbers()
    {
        Assert.Equal("+0.02 BPM", English(() => PracticeText.Bpm(0.0201)));
        Assert.Equal("+0,02 BPM", Spanish(() => PracticeText.Bpm(0.0201)));
        Assert.Equal("-0,10 BPM", Spanish(() => PracticeText.Bpm(-0.1)));
        Assert.Equal("0,00 BPM", Spanish(() => PracticeText.Bpm(-0.001)));
    }

    [Theory]
    [InlineData(46, "0:46")]
    [InlineData(70.4, "1:10")]
    [InlineData(3_665, "61:05")]
    [InlineData(-3, "0:00")]
    [InlineData(59.5, "1:00")]
    public void DurationsReadMinutesAndSeconds(double seconds, string expected)
    {
        Assert.Equal(expected, PracticeText.Duration(seconds));
    }

    [Fact]
    public void TheTrendSaysWhetherTheErrorWentDownUpOrStayed()
    {
        Assert.Equal("Down from 39 ms to 11 ms", English(() => PracticeText.Trend(39.2, 11)));
        Assert.Equal("Bajó de 39 ms a 11 ms", Spanish(() => PracticeText.Trend(39.2, 11)));
        Assert.Equal("Up from 11 ms to 39 ms", English(() => PracticeText.Trend(11, 39)));
        Assert.Equal("Steady at 12 ms", English(() => PracticeText.Trend(12.3, 11.6)));
    }

    [Fact]
    public void TheWaitingLineCountsTheSavedMixes()
    {
        Assert.Equal("Waiting for both decks to play", English(() => PracticeText.StatusWaiting(0)));
        Assert.Equal("Waiting for both decks · 1 mix saved", English(() => PracticeText.StatusWaiting(1)));
        Assert.Equal("Esperando ambos decks · 17 mezclas guardadas", Spanish(() => PracticeText.StatusWaiting(17)));
    }

    [Fact]
    public void TheLiveMixNamesItsDecksAndItsTempoMatch()
    {
        Assert.Equal("Deck 2 → Deck 1", English(() => PracticeText.DeckPair(2, 1)));
        Assert.Equal("Tempos ajustados en 0:11", Spanish(() => PracticeText.MatchedAfter(11)));
        Assert.Equal("Media de 9 ms desde que coinciden los tempos", Spanish(() => PracticeText.AverageSinceMatch(9)));
        Assert.Equal("Matching tempos · +0.30 BPM", English(() => PracticeText.Matching(0.3)));
        Assert.Equal("Matching tempos", English(() => PracticeText.Matching(null)));
        Assert.Equal("Cronometrando deck 2 → deck 1", Spanish(() => PracticeText.StatusMixing(2, 1)));
    }

    [Fact]
    public void DaysReadTodayOrAShortDate()
    {
        var zone = TimeZoneInfo.Utc;
        var now = new DateTimeOffset(2026, 11, 12, 18, 0, 0, TimeSpan.Zero);
        Assert.Equal("Today", English(() => PracticeText.ShortDay(now.AddHours(-17), now, zone)));
        Assert.Equal("Hoy", Spanish(() => PracticeText.ShortDay(now.AddHours(-17), now, zone)));
        Assert.Equal("Nov 1", English(() => PracticeText.ShortDay(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), now, zone)));
        Assert.Equal("1 nov", Spanish(() => PracticeText.ShortDay(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), now, zone)));
    }

    [Fact]
    public void TheLastMixLineNamesItsDecksEndAndLength()
    {
        var zone = TimeZoneInfo.Utc;
        var now = new DateTimeOffset(2026, 11, 12, 18, 0, 0, TimeSpan.Zero);
        var mix = new PracticeMix(Guid.Empty, now.AddMinutes(-11), now.AddMinutes(-10), 2, 1, 70, 13, 0.02, 11, 25);
        var line = Spanish(() => PracticeText.LastMixDetail(mix, now, zone));
        Assert.StartsWith("Deck 2 → Deck 1 · ", line);
        Assert.Contains("17:50", line);
        Assert.EndsWith(" · 1:10", line);
    }
}
