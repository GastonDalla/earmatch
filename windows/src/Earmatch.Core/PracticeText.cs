using System.Globalization;

namespace Earmatch.Core;

public static class PracticeText
{
    public const string NoValue = "—";

    private static CultureInfo Culture => L10n.Culture ?? CultureInfo.InvariantCulture;

    public static string Milliseconds(double value, bool signed = false)
    {
        var rounded = Numbers.Round(value);
        var number = rounded == 0 ? 0 : rounded;
        var text = number.ToString("#,0", Culture);
        return (signed && number > 0 ? "+" + text : text) + " ms";
    }

    public static string Bpm(double value)
    {
        var rounded = Numbers.Round(value * 100) / 100;
        var number = rounded == 0 ? 0 : rounded;
        var text = number.ToString("N2", Culture);
        return (number > 0 ? "+" + text : text) + " BPM";
    }

    public static string Duration(double seconds)
    {
        var total = (long)Math.Max(0, Numbers.Round(seconds));
        return $"{total / 60}:{total % 60:00}";
    }

    public static string DeckPair(int outgoing, int incoming) => L10n.Format("Deck %ld → Deck %ld", outgoing, incoming);

    public static string MatchedAfter(double seconds) => L10n.Format("Tempos matched in %@", Duration(seconds));

    public static string Matching(double? difference) =>
        difference is { } value ? L10n.Format("Matching tempos · %@", Bpm(value)) : L10n.Text("Matching tempos");

    public static string AverageSinceMatch(double? milliseconds) =>
        milliseconds is { } value
            ? L10n.Format("Average %@ since the tempos matched", Milliseconds(value))
            : L10n.Text("Average appears once the tempos match");

    public static string WorstAt(double seconds) => L10n.Format("Worst at %@", Duration(seconds));

    public static string ReplayPosition(double seconds, double total) => $"{Duration(seconds)} / {Duration(total)}";

    public static string Trend(double first, double last)
    {
        var change = Numbers.Round(last) - Numbers.Round(first);
        if (change < 0) return L10n.Format("Down from %@ to %@", Milliseconds(first), Milliseconds(last));
        if (change > 0) return L10n.Format("Up from %@ to %@", Milliseconds(first), Milliseconds(last));
        return L10n.Format("Steady at %@", Milliseconds(last));
    }

    public static string StatusMixing(int outgoing, int incoming) => L10n.Format("Timing deck %ld → deck %ld", outgoing, incoming);

    public static string StatusWaiting(int savedMixes) =>
        savedMixes == 0 ? L10n.Text("Waiting for both decks to play") : L10n.Plural("Waiting for both decks · %ld mixes saved", savedMixes);

    public static string ShortDay(DateTimeOffset day, DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(day, zone).DateTime;
        if (local.Date == TimeZoneInfo.ConvertTime(now, zone).DateTime.Date) return L10n.Text("Today");
        var culture = Culture;
        var month = culture.DateTimeFormat.GetAbbreviatedMonthName(local.Month).TrimEnd('.');
        var monthFirst = culture.TwoLetterISOLanguageName is "en" or "iv";
        return monthFirst ? $"{month} {local.Day}" : $"{local.Day} {month}";
    }

    public static string EndTime(DateTimeOffset date, DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(date, zone).DateTime;
        var time = local.ToString(Culture.DateTimeFormat.ShortTimePattern, Culture);
        return local.Date == TimeZoneInfo.ConvertTime(now, zone).DateTime.Date ? time : $"{ShortDay(date, now, zone)}, {time}";
    }

    public static string LastMixDetail(PracticeMix mix, DateTimeOffset now, TimeZoneInfo zone) =>
        $"{DeckPair(mix.OutgoingDeck, mix.IncomingDeck)} · {EndTime(mix.EndedAt, now, zone)} · {Duration(mix.BothPlayingSeconds)}";
}
