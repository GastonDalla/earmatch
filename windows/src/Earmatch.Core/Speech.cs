namespace Earmatch.Core;

public static class MeterSpeech
{
    public static string NoTrack => L10n.Text("no track loaded");
    public static string SyncMaster => L10n.Text("Sync Master");

    public static string Deck(int number) => L10n.Format("Deck %ld", number);
    public static string Beat(int beatInBar) => L10n.Format("beat %ld of 4", beatInBar + 1);
    public static string Counter(string counter) => L10n.Format("%@ bars", counter);
    public static string OutOfPhase(int deck) => L10n.Format("out of phase with deck %ld", deck);

    public static string DeckRow(LiveDeckSnapshot deck, bool showsMeter)
    {
        var parts = new List<string>(6) { Deck(deck.Number), deck.Title ?? NoTrack };
        if (deck.IsMaster) parts.Add(SyncMaster);
        if (showsMeter && deck.Meter is { } meter && OwnRow(meter) is { } own)
        {
            if (own.BeatInBar is { } beat)
            {
                parts.Add(Beat(beat));
                if (own.IsOnMastersBeat == false) parts.Add(OutOfPhase(meter.MasterPlayer ?? 0));
            }
            parts.Add(Counter(own.Counter));
        }
        return string.Join(", ", parts);
    }

    private static PhaseMeterRow? OwnRow(PhaseMeterDisplay meter)
    {
        foreach (var row in meter.Rows)
        {
            if (row.Role == PhaseMeterRole.Deck) return row;
        }
        return null;
    }
}

public static class PracticeSpeech
{
    public static string Live(PracticeLiveStatus live)
    {
        var parts = new List<string>(3) { L10n.Format("Mixing deck %ld into deck %ld for %@", live.OutgoingDeck, live.IncomingDeck, Duration(live.ElapsedSeconds)) };
        if (live.SecondsToMatch is { } matched) parts.Add(L10n.Format("tempos matched after %@", Duration(matched)));
        else if (live.TempoDifferenceBPM is { } difference) parts.Add(L10n.Format("tempos %@ apart", PracticeText.Bpm(Math.Abs(difference))));
        if (live.PhaseErrorMilliseconds is { } error) parts.Add(L10n.Format("phase error %@", Error(error)));
        return string.Join(". ", parts);
    }

    public static string Summary(PracticeMix mix)
    {
        var parts = new List<string>(5)
        {
            L10n.Format("Last mix, %@, %@ long", PracticeText.DeckPair(mix.OutgoingDeck, mix.IncomingDeck), Duration(mix.BothPlayingSeconds)),
            mix.SecondsToMatch is { } match ? L10n.Format("tempos matched after %@", Duration(match)) : L10n.Text("tempos never matched"),
        };
        if (mix.TempoDriftBPM is { } drift) parts.Add(L10n.Format("drift %@", PracticeText.Bpm(drift)));
        if (mix.MeanPhaseErrorMilliseconds is { } average) parts.Add(L10n.Format("average phase error %@", PracticeText.Milliseconds(average)));
        if (mix.WorstPhaseErrorMilliseconds is { } worst) parts.Add(L10n.Format("worst %@", PracticeText.Milliseconds(worst)));
        return string.Join(", ", parts);
    }

    public static string Replay(PracticeTrace trace)
    {
        var parts = new List<string>(3) { L10n.Format("Phase error of the last transition, %@ long", Duration(trace.DurationSeconds)) };
        if (trace.MatchedAtSeconds is { } matchedAt) parts.Add(L10n.Format("tempos matched at %@", PracticeText.Duration(matchedAt)));
        if (trace.WorstSample is { } worst)
            parts.Add(L10n.Format("worst %@ at %@", Error(worst.ErrorMilliseconds), PracticeText.Duration(worst.Seconds)));
        return string.Join(", ", parts);
    }

    public static string ReplayValue(double seconds, double? error) =>
        error is { } value ? $"{PracticeText.Duration(seconds)}, {Error(value)}" : PracticeText.Duration(seconds);

    public static string Error(double milliseconds)
    {
        if (Numbers.Round(milliseconds) == 0) return L10n.Text("in phase");
        var magnitude = PracticeText.Milliseconds(Math.Abs(milliseconds));
        return milliseconds > 0 ? L10n.Format("%@ ahead", magnitude) : L10n.Format("%@ behind", magnitude);
    }

    public static string Duration(double seconds)
    {
        var total = (long)Math.Max(0, Numbers.Round(seconds));
        var minutes = (int)(total / 60);
        var rest = (int)(total % 60);
        if (minutes == 0) return L10n.Plural("%ld seconds", rest);
        if (rest == 0) return L10n.Plural("%ld minutes", minutes);
        return L10n.Format("%@ and %@", L10n.Plural("%ld minutes", minutes), L10n.Plural("%ld seconds", rest));
    }
}
