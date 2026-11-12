using System.Text.RegularExpressions;

namespace Earmatch.Core;

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

    public RekordboxLayout Layout { get; }

    public IReadOnlyList<int> WatchedElements { get; }

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

    public static TimeSpan FullReadInterval(TimeSpan readDuration) =>
        TimeSpan.FromMilliseconds(Math.Clamp(readDuration.TotalMilliseconds * 10, 1000, 3000));

    public static bool IsTransientRead(RekordboxLayout layout) =>
        layout.Mode == RekordboxMode.Unknown && layout.LayoutName is null && layout.Decks.Count == 0;

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
        public static LiveDecksSnapshot Of(RekordboxLayout layout)
        {
            var shown = layout.Decks.Count > 2 ? layout.Decks.Where(deck => deck.BPM is not null || deck.Title is not null) : layout.Decks;
            return new LiveDecksSnapshot(shown.Select(deck => new LiveDeckSnapshot(deck.Number, deck.Title, IsMaster: false, Meter: null)).ToValueList(),
                0, 0);
        }
    }
}

public static partial class RekordboxProduct
{
    public const string ProcessName = "rekordbox";

    public static string? ShortVersion(string? productVersion)
    {
        var match = VersionPattern().Match(productVersion ?? "");
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"[0-9]+(\.[0-9]+){0,2}")]
    private static partial Regex VersionPattern();
}
