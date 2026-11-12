namespace Earmatch.Core;

public enum RekordboxMode
{
    Performance,
    Export,
    Edit,
    Unknown,
}

public enum StripOrientation
{
    Horizontal,
    Vertical,
}

public enum DeckArrangement
{
    TwoDeckHorizontal,
    TwoDeckVertical,
    FourDeckHorizontal,
    FourDeckVertical,
}

public static class DeckArrangementMembers
{
    extension(DeckArrangement arrangement)
    {
        /// The layout name rekordbox shows in its layout menu.
        public string RawValue => arrangement switch
        {
            DeckArrangement.TwoDeckHorizontal => "2Deck Horizontal",
            DeckArrangement.TwoDeckVertical => "2Deck Vertical",
            DeckArrangement.FourDeckHorizontal => "4Deck Horizontal",
            _ => "4Deck Vertical",
        };

        public int DeckCount => arrangement is DeckArrangement.TwoDeckHorizontal or DeckArrangement.TwoDeckVertical ? 2 : 4;

        public StripOrientation Orientation =>
            arrangement is DeckArrangement.TwoDeckHorizontal or DeckArrangement.FourDeckHorizontal ? StripOrientation.Horizontal : StripOrientation.Vertical;

        /// The decks of the waveform strips, in strip order (top to bottom, or left to right).
        public IReadOnlyList<int> StripDecks => arrangement == DeckArrangement.FourDeckVertical ? [3, 1, 2, 4] : Enumerable.Range(1, arrangement.DeckCount).ToList();
    }

    extension(DeckArrangement)
    {
        public static IReadOnlyList<DeckArrangement> AllCases => Enum.GetValues<DeckArrangement>();

        public static DeckArrangement? FromRawValue(string? rawValue)
        {
            foreach (var arrangement in Enum.GetValues<DeckArrangement>())
            {
                if (arrangement.RawValue == rawValue) return arrangement;
            }
            return null;
        }
    }
}

public sealed record WaveformStrip
{
    public WaveformStrip(ScreenRect frame, StripOrientation orientation, ScreenRect? markerArea = null)
    {
        Frame = frame;
        Orientation = orientation;
        MarkerArea = markerArea ?? frame;
    }

    public ScreenRect Frame { get; }
    public StripOrientation Orientation { get; }

    /// Where the beat markers are drawn: the whole strip, or in vertical layouts the part under the button bar.
    public ScreenRect MarkerArea { get; }

    public double Center => Orientation == StripOrientation.Horizontal ? Frame.MidX : Frame.MidY;
}

public sealed record DeckLayout
{
    public DeckLayout(int number, string? title, double? bpm, ScreenRect? headerBPMFrame, ScreenRect? jogBPMFrame, ScreenRect? pitchFrame,
        ScreenRect masterButtonFrame, WaveformStrip? strip, ScreenRect? overviewFrame, string? barsText = null, double? elapsedSeconds = null,
        double? remainingSeconds = null)
    {
        Number = number;
        Title = title;
        BPM = bpm;
        HeaderBPMFrame = headerBPMFrame;
        JogBPMFrame = jogBPMFrame;
        PitchFrame = pitchFrame;
        MasterButtonFrame = masterButtonFrame;
        Strip = strip;
        OverviewFrame = overviewFrame;
        BarsText = barsText;
        ElapsedSeconds = elapsedSeconds;
        RemainingSeconds = remainingSeconds;
    }

    public int Number { get; init; }
    public string? Title { get; init; }
    public double? BPM { get; set; }
    public ScreenRect? HeaderBPMFrame { get; init; }
    public ScreenRect? JogBPMFrame { get; init; }
    public ScreenRect? PitchFrame { get; init; }
    public ScreenRect MasterButtonFrame { get; init; }
    public WaveformStrip? Strip { get; init; }
    public ScreenRect? OverviewFrame { get; init; }
    public string? BarsText { get; set; }
    public double? ElapsedSeconds { get; set; }
    public double? RemainingSeconds { get; set; }

    public ScreenRect? WaveformStrip => Strip?.Frame;
}

/// Where rekordbox's decks, waveforms and BPM displays are on screen, read from its UI Automation tree.
public sealed class RekordboxLayout : IEquatable<RekordboxLayout>
{
    public RekordboxLayout(RekordboxMode mode, string? layoutName, ScreenRect? stackedWaveform, IEnumerable<DeckLayout> decks)
    {
        Mode = mode;
        LayoutName = layoutName;
        StackedWaveform = stackedWaveform;
        Decks = decks.ToList();
    }

    public RekordboxMode Mode { get; }
    public string? LayoutName { get; }
    public ScreenRect? StackedWaveform { get; }
    public List<DeckLayout> Decks { get; }

    public DeckArrangement? Arrangement => DeckArrangement.FromRawValue(LayoutName);

    public bool IsSupportedPerformanceLayout =>
        Mode == RekordboxMode.Performance && Arrangement is { } arrangement && StackedWaveform is not null
        && Decks.Count == arrangement.DeckCount && Decks.All(deck => deck.Strip is not null);

    public static RekordboxLayout Locate(RekordboxWindowSnapshot snapshot) => RekordboxLocator.Locate(snapshot);

    /// For each deck with a header BPM, the index of its text in the snapshot, to re-read between full reads.
    public Dictionary<int, int> HeaderBPMElementIndices(RekordboxWindowSnapshot snapshot)
    {
        var indices = new Dictionary<int, int>();
        foreach (var deck in Decks)
        {
            if (deck.HeaderBPMFrame is not { } frame) continue;
            for (var index = 0; index < snapshot.Elements.Count; index++)
            {
                var element = snapshot.Elements[index];
                if (element.ControlType != UiaControlType.Text || element.Frame != frame) continue;
                indices[deck.Number] = index;
                break;
            }
        }
        return indices;
    }

    /// Refreshes the decks' BPM from their header texts: a missing text keeps the BPM, an unreadable one clears it.
    public void ApplyLiveHeaderBPM(IReadOnlyDictionary<int, string> texts)
    {
        foreach (var deck in Decks)
        {
            if (texts.TryGetValue(deck.Number, out var text)) deck.BPM = BPMDisplayFormatter.Parse(text);
        }
    }

    public bool Equals(RekordboxLayout? other) =>
        other is not null && Mode == other.Mode && LayoutName == other.LayoutName && StackedWaveform == other.StackedWaveform
        && Decks.SequenceEqual(other.Decks);

    public override bool Equals(object? obj) => Equals(obj as RekordboxLayout);

    public override int GetHashCode() => HashCode.Combine(Mode, LayoutName, StackedWaveform, Decks.Count);
}
