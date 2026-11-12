using Earmatch.Core;

namespace Earmatch.App;

/// The main window's and the tray menu's texts. Keys are the macOS English texts; where macOS names its keys
/// (⌘, ⌥, Command, Option) Windows has its own key with the Windows shortcuts.
public static class WindowText
{
    public const string ToggleKeys = "Ctrl+Alt+Win+E";
    public const string SectionKeys = "Ctrl+1 – Ctrl+4";

    public static string LiveDecks => L10n.Text("LIVE DECKS");
    public static string BpmPrecisionSection => L10n.Text("BPM PRECISION");
    public static string WaveformSection => L10n.Text("WAVEFORM");
    public static string PhaseMeterSection => L10n.Text("PHASE METER");
    public static string HotkeysSection => L10n.Text("HOTKEYS");
    public static string LanguageSection => L10n.Text("LANGUAGE");
    public static string Language => L10n.Text("Language");
    public static string Enable => L10n.Text("Enable Earmatch");
    public static string EnableHint => L10n.Text("Turns every overlay on rekordbox on or off. Shortcut: Control Alt Windows E.");
    public static string EnableHelp => KeepTogether(L10n.Text("Turns every overlay on rekordbox on or off (Ctrl+Alt+Win+E)"));
    public static string LimitBPM => L10n.Text("Limit BPM display");
    public static string LimitBPMSubtitle => L10n.Text("Covers the jog BPM's digits, so you match by ear");
    public static string BpmPrecision => L10n.Text("BPM precision");
    public static string SampleHidden => L10n.Text("Sample: BPM hidden");
    public static string Waveform => L10n.Text("Waveform");
    public static string PhaseMeter => L10n.Text("Phase meter");
    public static string PhaseMeterSubtitle => L10n.Text("Pioneer-style beat meter on each deck");
    public static string PhaseMeterType => L10n.Text("Phase meter type");
    public static string RightAltKey => L10n.Text("Right Alt");
    public static string PeekHint => L10n.Text("Hold to see rekordbox unchanged");
    public static string ToggleHint => L10n.Text("Turn Earmatch on or off");
    public static string SwitchSectionsHint => L10n.Text("Switch between sections");
    public static string HotkeysSpoken => L10n.Text("Hotkeys: hold Right Alt to see rekordbox unchanged; Control Alt Windows E turns Earmatch on or off");
    public static string CopyDiagnostics => L10n.Text("Copy Diagnostics");
    public static string CopyDiagnosticsHelp => L10n.Text("Copy Diagnostics: copies a plain-text report (versions, status, settings) to attach to a bug report");
    public static string Copied => L10n.Text("Copied");
    public static string CopyFailed => L10n.Text("Couldn't Copy");
    public static string CopyFailedNotice => L10n.Text("Couldn't copy the diagnostics: another app is using the clipboard. Try again in a moment.");
    public static string Minimize => L10n.Text("Minimize");
    public static string Maximize => L10n.Text("Maximize");
    public static string Restore => L10n.Text("Restore");
    public static string Close => L10n.Text("Close");
    public static string Quit => L10n.Text("Quit Earmatch");
    public static string Sections => L10n.Text("Sections");
    public static string Independence => L10n.Text("Independent utility. Not affiliated with AlphaTheta, Pioneer DJ or rekordbox.");
    public static string DevelopmentBuild => L10n.Text("development build");
    public static string ToggleSwitch => L10n.Text("toggle switch");

    public static string Sample(string visible) => L10n.Format("Sample: BPM reads %@", visible);
    public static string Status(string title) => L10n.Format("Status: %@", title);
    public static string Shortcut(int number) => L10n.Format("Shortcut: Control %ld", number);

    public static string PreviewTitle => L10n.Text("PREVIEW");
    public static string PeekHelp => L10n.Text("Hold to see rekordbox unchanged, as holding Right Alt does");
    public static string Peeking => L10n.Text("Showing rekordbox unchanged");
    public static string Peek => L10n.Text("Peek at rekordbox unchanged");
    public static string Unchanged => L10n.Text("rekordbox unchanged");
    public static string LiveChip => L10n.Text("LIVE");
    public static string LiveSpoken => L10n.Text("Live");

    public static string NoTrack => L10n.Text("No track loaded");
    public static string MeterOff => L10n.Text("Phase meter off");
    public static string ReadingGrid => L10n.Text("Reading the beat grid…");
    public static string EmptyDecksHint => L10n.Text("Both decks show here with their phase meters while rekordbox plays in front.");
    public static string SyncMasterChip => L10n.Text("SYNC MASTER");

    public static string WaveformNormalPreview => L10n.Text("Preview: rekordbox's stacked waveforms");
    public static string WaveformSplitPreview => L10n.Text("Preview: each deck's waveform in its own half with its own playhead");
    public static string WaveformHiddenPreview => L10n.Text("Preview: both waveforms hidden");

    /// Joins the shortcut's keys with word joiners so a wrapped line never splits "Ctrl+Alt+Win+E".
    public static string KeepTogether(string text) => text.Replace(ToggleKeys, string.Join("\u2060+\u2060", ToggleKeys.Split('+')), StringComparison.Ordinal);

    public static string CounterHelp(CounterStyle style) => $"{style.Title}: {style.Explanation}";
}

/// The credit line of the About card and the tray menu.
public static class Credit
{
    public const string Author = "GastonDalla";
    public const string Url = "https://github.com/GastonDalla";
    public const string DisplayUrl = "github.com/GastonDalla";

    public static string Line => L10n.Format("Made by %@", Author);
    public static string MenuLine => $"{Line} · {DisplayUrl}";
    public static string Help => L10n.Format("Made by %@. Opens %@ in your browser", Author, DisplayUrl);

    /// The line around the author's name, which the About card shows in the accent colour.
    public static (string Before, string After) Parts
    {
        get
        {
            var line = Line;
            var index = line.IndexOf(Author, StringComparison.Ordinal);
            return index < 0 ? (line + " ", "") : (line[..index], line[(index + Author.Length)..]);
        }
    }
}

public static class PracticeStrings
{
    public static string Title => L10n.Text("Last mixes");
    public static string Subtitle => L10n.Text("Times every mix you make by ear");
    public static string WaitingTitle => L10n.Text("Waiting for a mix");
    public static string WaitingBody => L10n.Text("Play both decks together. Each mix of 8 seconds or more is timed and saved.");
    public static string EmptyTitle => L10n.Text("No mixes yet");
    public static string EmptyBody => L10n.Text("Your mixes show up here with a replay of the last one and your progress day by day.");
    public static string LastMix => L10n.Text("LAST MIX");
    public static string Replay => L10n.Text("REPLAY");
    public static string Progress => L10n.Text("PROGRESS");
    public static string TempoMatch => L10n.Text("Tempo match");
    public static string Drift => L10n.Text("Drift");
    public static string AverageError => L10n.Text("Avg error");
    public static string WorstError => L10n.Text("Worst");
    public static string NotMatched => L10n.Text("Not matched");
    public static string Behind => L10n.Text("BEHIND");
    public static string Ahead => L10n.Text("AHEAD");
    public static string Play => L10n.Text("Replay");
    public static string Stop => L10n.Text("Stop");
    public static string PlayHint => L10n.Text("Replays the phase error of the last transition");
    public static string StopHint => L10n.Text("Stops the replay");
    public static string MedianPerDay => L10n.Text("Median phase error per day");
    public static string WaitingForTempo => L10n.Text("Saved once the decks play 8 seconds together");
    public static string NoPhase => L10n.Text("Reading the beat grids…");
}

public static class MenuText
{
    public static string LimitBPM => L10n.Text("Limit BPM Display");
    public static string BpmPrecision => L10n.Text("BPM Precision");
    public static string OneDecimal => L10n.Text("One Decimal");
    public static string NoDecimals => L10n.Text("No Decimals");
    public static string CoverFully => L10n.Text("Cover Fully");
    public static string PhaseMeter => L10n.Text("Phase Meter");
    public static string Open => L10n.Text("Open Earmatch…");
    public static string PeekNote => L10n.Text("Hold Right Alt to peek · Ctrl+Alt+Win+E turns Earmatch on or off");
    public static string About => L10n.Text("About Earmatch");
    public static string Independence => L10n.Text("Independent utility, not affiliated with AlphaTheta, Pioneer DJ or rekordbox");
}
