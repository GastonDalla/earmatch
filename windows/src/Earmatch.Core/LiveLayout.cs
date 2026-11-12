using System.Text.RegularExpressions;

namespace Earmatch.Core;

/// What the rekordbox monitor keeps between full reads of rekordbox's window (LiveWindowReader in
/// RekordboxMonitor.swift): the layout located in the last full read, and the elements whose values change while a
/// track plays (each deck's header BPM, elapsed and remaining time), which the monitor re-reads on every poll.
public sealed partial class LiveLayout
{
    private readonly Dictionary<int, int> _headerBPMs;
    private readonly HashSet<int> _times;
    private readonly List<UiaElementSnapshot> _timeElements;
    private RekordboxLayout _timeLayout;

    public LiveLayout(RekordboxWindowSnapshot snapshot)
    {
        Snapshot = snapshot;
        Layout = RekordboxLayout.Locate(snapshot);
        _headerBPMs = Layout.HeaderBPMElementIndices(snapshot);
        _times = TimeElementIndices(snapshot, Layout);
        _timeElements = snapshot.Elements.ToList();
        _timeLayout = Layout;
        WatchedElements = _headerBPMs.Values.Concat(_times).Distinct().Order().ToList();
    }

    public RekordboxWindowSnapshot Snapshot { get; }

    /// The layout as the full read located it.
    public RekordboxLayout Layout { get; }

    /// The indices in Snapshot.Elements of the elements to read again on every poll, in tree order.
    public IReadOnlyList<int> WatchedElements { get; }

    /// The layout with the values just read: element index to its value. An element missing from `values` keeps the
    /// value it had (its read failed); a header BPM that does not read as a BPM clears its deck's BPM. Times build on
    /// the times of earlier refreshes. The returned layout is new: earlier layouts never change.
    public RekordboxLayout Refresh(IReadOnlyDictionary<int, string> values)
    {
        var timesChanged = false;
        foreach (var index in _times)
        {
            if (!values.TryGetValue(index, out var text)) continue;
            var value = text.Length == 0 ? null : text;
            if (_timeElements[index].Value == value) continue;
            _timeElements[index] = _timeElements[index] with { Value = value };
            timesChanged = true;
        }
        if (timesChanged) _timeLayout = RekordboxLayout.Locate(Snapshot with { Elements = _timeElements.ToList() });

        var decks = Layout.Decks.Select(deck =>
        {
            var bpm = _headerBPMs.TryGetValue(deck.Number, out var header) && values.TryGetValue(header, out var text)
                ? BPMDisplayFormatter.Parse(text)
                : deck.BPM;
            var times = _timeLayout.Decks.FirstOrDefault(located => located.Number == deck.Number);
            return deck with
            {
                BPM = bpm,
                ElapsedSeconds = times is null ? deck.ElapsedSeconds : times.ElapsedSeconds,
                RemainingSeconds = times is null ? deck.RemainingSeconds : times.RemainingSeconds,
            };
        });
        return new RekordboxLayout(Layout.Mode, Layout.LayoutName, Layout.StackedWaveform, decks, Layout.Scale);
    }

    /// How long to wait before reading the whole window again after a full read that took `readDuration`: once a
    /// second like the macOS monitor, and less often when a read takes longer than a tenth of that (rekordbox answers
    /// UI Automation on its own window thread), up to every 3 seconds. A resized window or a closed popup menu is read
    /// again at once anyway.
    public static TimeSpan FullReadInterval(TimeSpan readDuration) =>
        TimeSpan.FromMilliseconds(Math.Clamp(readDuration.TotalMilliseconds * 10, 1000, 3000));

    /// Whether a full read caught rekordbox in a passing state rather than a new layout: while one of its popup menus
    /// is open (the layout or mode picker, a hot cue menu...) its window shows UI Automation only the title bar, so
    /// neither the mode box nor any deck is found. The monitor keeps the layout it had, as the macOS overlays stay up
    /// while such a menu is open.
    public static bool IsTransientRead(RekordboxLayout layout) =>
        layout.Mode == RekordboxMode.Unknown && layout.LayoutName is null && layout.Decks.Count == 0;

    /// The elapsed and remaining time texts in the decks' header rows ("00:07", "-01:23" and their ".3" tenths).
    private static HashSet<int> TimeElementIndices(RekordboxWindowSnapshot snapshot, RekordboxLayout layout)
    {
        var rows = layout.Decks.Select(deck => deck.MasterButtonFrame.MinY).ToList();
        var indices = new HashSet<int>();
        for (var index = 0; index < snapshot.Elements.Count; index++)
        {
            var element = snapshot.Elements[index];
            if (element.ControlType != UiaControlType.Text || element.Frame.Height > 18 * snapshot.Scale) continue;
            if (!rows.Any(row => Math.Abs(row - element.Frame.MinY) <= 6 * snapshot.Scale)) continue;
            if (element.Value is { } value && TimePattern().IsMatch(value)) indices.Add(index);
        }
        return indices;
    }

    [GeneratedRegex(@"^(-?[0-9]{2}:[0-9]{2}|\.[0-9])$")]
    private static partial Regex TimePattern();
}

public static class LiveDecksSnapshotLayout
{
    extension(LiveDecksSnapshot)
    {
        /// The live decks mirror of a layout read from UI Automation alone: each deck's number and title, without a
        /// phase meter, master or tempo (those come from the captured pixels). Like the macOS feed, a four-deck layout
        /// shows only its loaded decks.
        public static LiveDecksSnapshot Of(RekordboxLayout layout)
        {
            var shown = layout.Decks.Count > 2 ? layout.Decks.Where(deck => deck.BPM is not null || deck.Title is not null) : layout.Decks;
            return new LiveDecksSnapshot(shown.Select(deck => new LiveDeckSnapshot(deck.Number, deck.Title, IsMaster: false, Meter: null)).ToValueList(),
                0, 0);
        }
    }
}

/// rekordbox's executable.
public static partial class RekordboxProduct
{
    /// The process name of rekordbox for Windows (rekordbox.exe).
    public const string ProcessName = "rekordbox";

    /// The version the way rekordbox and the macOS app show it: the first three numbers of the file's product version
    /// ("7.2.19.0342" reads "7.2.19"); null without one.
    public static string? ShortVersion(string? productVersion)
    {
        var match = VersionPattern().Match(productVersion ?? "");
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"[0-9]+(\.[0-9]+){0,2}")]
    private static partial Regex VersionPattern();
}
