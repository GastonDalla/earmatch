using System.Globalization;

namespace Earmatch.Core;

public static class WindowRenderSample
{
    public const string RekordboxVersion = "7.2.4";
    public static IReadOnlyList<string> TrackTitles { get; } = ["Night Shift Signal", "Glasshouse Dub", "Concrete Bloom", "Tidewater Pulse"];

    private const double Bpm = 128;
    private const int MasterDeck = 2;
    private static readonly Dictionary<int, double> Phases = new() { [1] = 2.46, [2] = 2.5, [3] = 1.93, [4] = 2.53 };
    private static readonly Dictionary<int, int> Bars = new() { [1] = 17, [2] = 49, [3] = 33, [4] = 9 };

    public static OverlaySettings Settings
    {
        get
        {
            var settings = new OverlaySettings();
            BoothPreset.Cdj3000.Apply(ref settings);
            return settings;
        }
    }

    public static LiveDecksSnapshot Decks(int count)
    {
        var numbers = Enumerable.Range(1, Math.Clamp(count, 1, TrackTitles.Count)).ToList();
        var inputs = numbers.Select(number =>
        {
            var phase = Phases.GetValueOrDefault(number);
            return new PhaseMeterDeckInput(number, phase, new BarPosition(Bars.GetValueOrDefault(number, 1), (int)phase + 1));
        }).ToList();
        var style = Settings.PhaseMeterType;
        var decks = numbers.Select(number => new LiveDeckSnapshot(number, TrackTitles[number - 1], number == MasterDeck,
            PhaseMeterModel.Display(number, inputs, MasterDeck, style), Phases[number], Bpm / 60));
        return new LiveDecksSnapshot(decks.ToValueList(), FramesPerSecond: 60, LatencyMilliseconds: 7.4);
    }

    public static StatusContext StatusContext(int deckCount)
    {
        var layout = deckCount > 2 ? DeckArrangement.FourDeckHorizontal : DeckArrangement.TwoDeckHorizontal;
        return new StatusContext(OverlayActivation.Active, Settings, RekordboxVersion, captureProblem: null, layoutName: layout.RawValue);
    }

    public static PracticeLiveStatus PracticeLive { get; } = new(
        OutgoingDeck: 2, IncomingDeck: 1, ElapsedSeconds: 46, BothPlayingSeconds: 46, TempoDifferenceBPM: 0.01, SecondsToMatch: 11,
        PhaseErrorMilliseconds: 6, MeanPhaseErrorMilliseconds: 9);

    public static PracticeOverview PracticeOverview(DateTimeOffset now, TimeZoneInfo zone)
    {
        (int DaysAgo, double[] Errors)[] dailyErrors =
        [
            (11, [41, 37]), (10, [36]), (8, [33, 29, 31]), (7, [27]), (5, [24, 21]),
            (4, [19, 22]), (2, [16, 14, 17]), (1, [13]), (0, [12, 9, 11]),
        ];
        var today = PracticeProgress.StartOfDay(now, zone);
        var todayDate = TimeZoneInfo.ConvertTime(today, zone).DateTime.Date;
        var mixes = new List<PracticeMix>();
        for (var dayIndex = 0; dayIndex < dailyErrors.Length; dayIndex++)
        {
            var (daysAgo, errors) = dailyErrors[dayIndex];
            var dayDate = todayDate.AddDays(-daysAgo);
            var day = new DateTimeOffset(dayDate, zone.GetUtcOffset(dayDate));
            for (var mixIndex = 0; mixIndex < errors.Length; mixIndex++)
            {
                var ended = daysAgo == 0
                    ? Min(now, Max(today.AddSeconds(mixIndex + 1), now.AddSeconds(-240 - (errors.Length - 1 - mixIndex) * 300)))
                    : day.AddSeconds(19 * 3600 + mixIndex * 900);
                var length = 52 + (dayIndex + mixIndex) % 4 * 9.0;
                var isEven = mixIndex % 2 == 0;
                mixes.Add(new PracticeMix(SampleID(dayIndex * 10 + mixIndex), ended.AddSeconds(-length), ended,
                    OutgoingDeck: isEven ? 2 : 1, IncomingDeck: isEven ? 1 : 2, BothPlayingSeconds: length,
                    SecondsToMatch: 26 - dayIndex * 1.6, TempoDriftBPM: 0.04 - dayIndex * 0.003,
                    MeanPhaseErrorMilliseconds: errors[mixIndex], WorstPhaseErrorMilliseconds: errors[mixIndex] * 2.3));
            }
        }
        var last = mixes[^1];
        return Core.PracticeOverview.From(new PracticeHistory(mixes, Trace(last)), zone);
    }

    private static PracticeTrace Trace(PracticeMix mix)
    {
        var duration = mix.BothPlayingSeconds;
        var matched = mix.SecondsToMatch ?? 0;
        var samples = new List<PracticeTraceSample>();
        for (var step = 0; step * 0.25 <= duration; step++)
        {
            var seconds = step * 0.25;
            var error = seconds < matched
                ? 62 * Math.Cos(seconds * 0.9) * (1 - seconds / matched * 0.6) + 18 * Math.Sin(seconds * 3.1)
                : 8 * Math.Sin(seconds * 0.7) + 5 * Math.Sin(seconds * 2.3 + 1) - 2;
            samples.Add(new PracticeTraceSample(seconds, Numbers.Round(error * 10) / 10));
        }
        return new PracticeTrace(mix.Id, duration, matched, samples);
    }

    private static Guid SampleID(int index) =>
        Guid.Parse("00000000-0000-4000-8000-" + index.ToString("D12", CultureInfo.InvariantCulture));

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
