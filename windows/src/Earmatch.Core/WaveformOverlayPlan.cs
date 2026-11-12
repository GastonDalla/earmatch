namespace Earmatch.Core;

/// One deck's part of the split or hidden waveform panel: its frame in the panel, the height of the waveform it shows,
/// the strip area it copies from the frame (screen coordinates) and whether it carries the deck's phase meter.
public sealed record WaveformOverlayPane(int Deck, ScreenRect Frame, double WaveformHeight, ScreenRect Source, bool ShowsMeter);

/// What the waveform overlay draws over rekordbox's stacked waveform for a waveform mode. Horizontal layouts: Split
/// (and Hide Both) cut the panel into a pane per deck with dividers, Normal lays a meter band over each strip. Vertical
/// layouts keep their columns (Hide Both blacks them out) and show the meters beside them, on each deck's overview.
/// Pane, divider and band frames are relative to the panel; the panel, sources and side meters are screen coordinates.
/// Distances the macOS code keeps in points (the divider, the band rounding) follow the layout's scale.
public sealed class WaveformOverlayPlan : IEquatable<WaveformOverlayPlan>
{
    /// The divider between panes, in points.
    public const double DividerWidth = 2;
    internal const double BandShare = 0.6;

    private WaveformOverlayPlan(ScreenRect? panel, ValueList<WaveformOverlayPane> panes, ValueList<ScreenRect> dividers,
        Dictionary<int, ScreenRect> bands, Dictionary<int, ScreenRect> sideMeters, bool allowsMasterWaveform)
    {
        Panel = panel;
        Panes = panes;
        Dividers = dividers;
        Bands = bands;
        SideMeters = sideMeters;
        AllowsMasterWaveform = allowsMasterWaveform;
    }

    public ScreenRect? Panel { get; }
    public ValueList<WaveformOverlayPane> Panes { get; }
    public ValueList<ScreenRect> Dividers { get; }
    public IReadOnlyDictionary<int, ScreenRect> Bands { get; }
    public IReadOnlyDictionary<int, ScreenRect> SideMeters { get; }
    public bool AllowsMasterWaveform { get; }

    /// The plan, or null when the layout has no stacked waveform or the mode draws nothing (Normal without meters).
    /// The band size is the meter band's size in screen units.
    public static WaveformOverlayPlan? Create(RekordboxLayout layout, WaveformMode mode, bool showsMeters, ScreenSize bandSize)
    {
        if (layout.Arrangement is not { } arrangement || layout.StackedWaveform is not { } stacked) return null;
        var strips = new Dictionary<int, ScreenRect>();
        foreach (var deck in layout.Decks)
        {
            if (deck.WaveformStrip is { } strip) strips[deck.Number] = strip;
            else strips.Remove(deck.Number);
        }
        var decks = layout.Decks.Select(deck => deck.Number).Where(strips.ContainsKey).ToList();
        var unit = layout.Scale;

        if (arrangement.Orientation == StripOrientation.Horizontal)
        {
            if (mode == WaveformMode.Normal && !showsMeters) return null;
            if (mode == WaveformMode.Normal)
            {
                var bands = new Dictionary<int, ScreenRect>();
                foreach (var number in decks)
                {
                    var strip = strips[number];
                    var height = Math.Min(bandSize.Height, Numbers.Round(strip.Height / unit * BandShare * 10) / 10 * unit);
                    bands[number] = new ScreenRect(strip.MidX - stacked.MinX - bandSize.Width / 2, strip.MinY - stacked.MinY, bandSize.Width, height);
                }
                return new WaveformOverlayPlan(stacked, [], [], bands, [], allowsMasterWaveform: false);
            }
            var divider = DividerWidth * unit;
            var rows = Math.Max(1, (arrangement.DeckCount + 1) / 2);
            var width = (stacked.Width - divider) / 2;
            var paneHeight = (stacked.Height - (rows - 1) * divider) / rows;
            var panes = decks.Select(number =>
            {
                var strip = strips[number];
                int column = (number - 1) % 2, row = (number - 1) / 2;
                var waveform = Math.Min(strip.Height, paneHeight);
                return new WaveformOverlayPane(number,
                    new ScreenRect(column * (width + divider), row * (paneHeight + divider), width, paneHeight),
                    waveform,
                    new ScreenRect(strip.MidX - width / 2, strip.MinY, width, waveform),
                    ShowsMeter: true);
            }).ToValueList();
            var dividers = new List<ScreenRect> { new(width, 0, divider, stacked.Height) };
            for (var row = 1; row < rows; row++) dividers.Add(new ScreenRect(0, row * (paneHeight + divider) - divider, stacked.Width, divider));
            return new WaveformOverlayPlan(stacked, panes, dividers, [], [], allowsMasterWaveform: mode == WaveformMode.Split);
        }

        var meters = new Dictionary<int, ScreenRect>();
        if (showsMeters)
        {
            foreach (var deck in layout.Decks)
            {
                if (deck.OverviewFrame is not { } overview) continue;
                double width = Math.Min(bandSize.Width, overview.Width), height = Math.Min(bandSize.Height, overview.Height);
                var x = overview.MidX < stacked.MidX ? overview.MaxX - width : overview.MinX;
                meters[deck.Number] = new ScreenRect(x, overview.MidY - height / 2, width, height);
            }
        }
        if (mode != WaveformMode.HideBoth && meters.Count == 0) return null;
        if (mode != WaveformMode.HideBoth) return new WaveformOverlayPlan(null, [], [], [], meters, allowsMasterWaveform: false);
        var hidden = decks.Select(number =>
            new WaveformOverlayPane(number, strips[number].Offset(-stacked.MinX, -stacked.MinY), 0, default, ShowsMeter: false)).ToValueList();
        return new WaveformOverlayPlan(stacked, hidden, [], [], meters, allowsMasterWaveform: false);
    }

    public bool Equals(WaveformOverlayPlan? other) =>
        other is not null && Panel == other.Panel && Panes.Equals(other.Panes) && Dividers.Equals(other.Dividers)
        && SameEntries(Bands, other.Bands) && SameEntries(SideMeters, other.SideMeters) && AllowsMasterWaveform == other.AllowsMasterWaveform;

    public override bool Equals(object? obj) => Equals(obj as WaveformOverlayPlan);

    public override int GetHashCode() => HashCode.Combine(Panel, Panes, Dividers, Bands.Count, SideMeters.Count, AllowsMasterWaveform);

    private static bool SameEntries(IReadOnlyDictionary<int, ScreenRect> one, IReadOnlyDictionary<int, ScreenRect> other) =>
        one.Count == other.Count && one.All(entry => other.TryGetValue(entry.Key, out var value) && value == entry.Value);
}
