using System.Text.RegularExpressions;

namespace Earmatch.Core;

/// Builds the RekordboxLayout from the UI Automation snapshot of rekordbox for Windows. It ports the macOS locator
/// (RekordboxLayout.swift): rekordbox for Windows draws the same JUCE layout as on macOS at the display scale, so every
/// distance below is the macOS distance in points times the snapshot's scale. Differences from the macOS tree:
/// - the waveform anchor is the zoom column's "RST" button (the macOS "Capture"/"Matching" buttons are absent);
/// - a deck's MASTER button is the one with the "Sync Master" help (the browser has a Traffic Light MASTER button);
/// - groups are Custom elements, static texts Text elements and the jog BPM an Edit (or Text) element;
/// - deck columns start at the window's left edge instead of x = 0.
internal static partial class RekordboxLocator
{
    private const double HeaderHeight = 40;

    public static RekordboxLayout Locate(RekordboxWindowSnapshot snapshot)
    {
        var units = new Units(snapshot.Scale);
        var elements = snapshot.Elements;
        var modeBox = elements.FirstOrDefault(element => element.ControlType == UiaControlType.ComboBox
            && HasHelpPrefix(element, "Change EXPORT/PERFORMANCE/EDIT mode"));
        var mode = modeBox is null ? RekordboxMode.Unknown : Mode(modeBox.Value);
        var layoutName = modeBox is null ? null : LayoutBox(modeBox, elements, units)?.Value;
        var waveform = DeckArrangement.FromRawValue(layoutName) is { } arrangement ? MainWaveform(elements, arrangement, units) : null;
        var decks = LocateDecks(elements, snapshot.WindowFrame, waveform?.Strips ?? [], units);
        foreach (var deck in decks)
        {
            if (deck.Strip is { Orientation: StripOrientation.Vertical } strip) deck.BarsText = BarsText(strip.Frame, elements, units);
        }
        return new RekordboxLayout(mode, layoutName, waveform?.Stacked, decks);
    }

    /// Converts the macOS point distances to the snapshot's pixels.
    private readonly record struct Units(double Scale)
    {
        public double this[double points] => points * Scale;
    }

    private sealed record Waveform(ScreenRect Stacked, Dictionary<int, WaveformStrip> Strips);

    private static RekordboxMode Mode(string? value) => value?.ToLowerInvariant() switch
    {
        "performance" => RekordboxMode.Performance,
        "export" => RekordboxMode.Export,
        "edit" => RekordboxMode.Edit,
        _ => RekordboxMode.Unknown,
    };

    private static UiaElementSnapshot? LayoutBox(UiaElementSnapshot mode, IReadOnlyList<UiaElementSnapshot> elements, Units units) =>
        elements
            .Where(element => element.ControlType == UiaControlType.ComboBox && Math.Abs(element.Frame.MidY - mode.Frame.MidY) <= units[8]
                && element.Frame.MinX >= mode.Frame.MaxX)
            .MinBy(element => element.Frame.MinX);

    private static Waveform? MainWaveform(IReadOnlyList<UiaElementSnapshot> elements, DeckArrangement arrangement, Units units)
    {
        var anchor = elements.FirstOrDefault(element => element.ControlType == UiaControlType.Button && element.Name == "RST");
        if (anchor is null) return null;
        var center = anchor.Frame.Center;
        var groups = elements.Where(element => element.ControlType == UiaControlType.Custom).Select(element => element.Frame).ToList();
        var holders = groups.Where(group => group.Contains(center)).ToList();
        if (holders.Count == 0) return null;
        var holder = holders.MinBy(group => group.Area);
        var count = arrangement.DeckCount;
        bool Share(double part, double whole) => part / whole > 0.75 / count && part / whole < 1.3 / count;
        ScreenRect stacked;
        List<ScreenRect> frames;
        if (arrangement.Orientation == StripOrientation.Horizontal)
        {
            if (holder.Width >= units[80]) return null;
            var sameRows = groups.Where(group => Math.Abs(group.MinY - holder.MinY) <= units[1] && Math.Abs(group.Height - holder.Height) <= units[1]).ToList();
            if (sameRows.Count == 0) return null;
            stacked = sameRows.MaxBy(group => group.Width);
            frames = Distinct(groups.Where(group => group.MinY >= stacked.MinY - units[1] && group.MaxY <= stacked.MaxY + units[1]
                && group.Width >= stacked.Width - units[30] && Share(group.Height, stacked.Height)), frame => frame.MinY, units);
        }
        else
        {
            List<ScreenRect> Columns(ScreenRect candidate) =>
                Distinct(groups.Where(group => Math.Abs(group.MinY - candidate.MinY) <= units[1] && Math.Abs(group.Height - candidate.Height) <= units[1]
                    && group.MinX >= candidate.MinX - units[1] && group.MaxX <= candidate.MaxX + units[1] && Share(group.Width, candidate.Width)),
                    frame => frame.MinX, units);
            var found = groups
                .Where(group => Math.Abs(group.MinX - holder.MinX) <= units[1] && Math.Abs(group.Width - holder.Width) <= units[1]
                    && group.Height > holder.Height && Columns(group).Count == count)
                .Cast<ScreenRect?>()
                .FirstOrDefault();
            if (found is not { } column) return null;
            stacked = column;
            frames = Columns(column);
        }
        if (frames.Count != count) return null;
        var strips = new Dictionary<int, WaveformStrip>();
        foreach (var (deck, frame) in arrangement.StripDecks.Zip(frames))
        {
            ScreenRect? markerArea = arrangement.Orientation == StripOrientation.Vertical && holder.MaxY > frame.MinY
                ? new ScreenRect(frame.MinX, holder.MaxY, frame.Width, frame.MaxY - holder.MaxY)
                : null;
            strips[deck] = new WaveformStrip(frame, arrangement.Orientation, markerArea);
        }
        return new Waveform(stacked, strips);
    }

    private static List<ScreenRect> Distinct(IEnumerable<ScreenRect> frames, Func<ScreenRect, double> key, Units units)
    {
        var result = new List<ScreenRect>();
        foreach (var frame in frames)
        {
            if (!result.Any(kept => Math.Abs(key(kept) - key(frame)) <= units[1])) result.Add(frame);
        }
        return result.OrderBy(key).ToList();
    }

    private static string? BarsText(ScreenRect strip, IReadOnlyList<UiaElementSnapshot> elements, Units units)
    {
        var text = elements.FirstOrDefault(element => element.ControlType == UiaControlType.Text && Math.Abs(element.Frame.MinY - strip.MaxY) <= units[6]
            && element.Frame.MidX >= strip.MinX && element.Frame.MidX < strip.MaxX);
        var value = Text.TrimWhitespaces(text?.Value ?? "");
        return value.Length == 0 ? null : value;
    }

    private static List<DeckLayout> LocateDecks(IReadOnlyList<UiaElementSnapshot> elements, ScreenRect window,
        Dictionary<int, WaveformStrip> strips, Units units)
    {
        var masters = elements
            .Where(element => element.ControlType == UiaControlType.Button && element.Name == "MASTER" && HasHelpPrefix(element, "Sync Master"))
            .OrderBy(element => element.Frame.MinY)
            .ToList();
        if (masters.Count is not (2 or 4)) return [];
        var rows = masters.Chunk(2).Select(row => row.OrderBy(element => element.Frame.MinX).ToList()).ToList();
        if (!rows.All(row => Math.Abs(row[0].Frame.MinY - row[1].Frame.MinY) <= units[6])) return [];
        var halfWidth = window.Width / 2;
        var decks = new List<DeckLayout>();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var top = rowIndex == 0 ? double.NegativeInfinity : row[0].Frame.MinY - units[HeaderHeight];
            var bottom = rowIndex + 1 < rows.Count ? rows[rowIndex + 1][0].Frame.MinY - units[HeaderHeight] : double.PositiveInfinity;
            for (var column = 0; column < row.Count; column++)
            {
                var number = rowIndex * 2 + column + 1;
                var left = window.MinX + column * halfWidth;
                var inDeck = elements.Where(element => element.Frame.MidX >= left && element.Frame.MidX < left + halfWidth
                    && element.Frame.MidY >= top && element.Frame.MidY < bottom).ToList();
                decks.Add(Deck(number, row[column], inDeck, strips.GetValueOrDefault(number), units));
            }
        }
        return decks;
    }

    private static DeckLayout Deck(int number, UiaElementSnapshot master, List<UiaElementSnapshot> elements, WaveformStrip? strip, Units units)
    {
        var texts = elements.Where(element => element.ControlType == UiaControlType.Text).ToList();
        var headerRow = texts.Where(text => Math.Abs(text.Frame.MinY - master.Frame.MinY) <= units[6] && text.Frame.Height <= units[18]).ToList();
        var header = headerRow.FirstOrDefault(text => string.IsNullOrEmpty(text.HelpText) && HeaderBPMPattern().IsMatch(text.Value ?? ""));
        var elapsed = Time(ElapsedPattern(), headerRow, units);
        var remaining = Time(RemainingPattern(), headerRow, units);
        var isLoaded = header is not null || (elapsed ?? 0) + (remaining ?? 0) > 0;
        var title = !isLoaded ? null : texts
            .Where(text => text.Frame.MaxY <= master.Frame.MinY + units[1] && text.Frame.MinY >= master.Frame.MinY - units[HeaderHeight]
                && !string.IsNullOrEmpty(text.Value))
            .MaxBy(text => text.Frame.Width)?
            .Value;
        var jog = elements.FirstOrDefault(element => element.ControlType is UiaControlType.Edit or UiaControlType.Text
            && HasHelpPrefix(element, "Deck BPM display"));
        return new DeckLayout(
            number: number,
            title: title,
            bpm: header is null ? null : BPMDisplayFormatter.Parse(header.Value ?? ""),
            headerBPMFrame: header?.Frame,
            jogBPMFrame: jog?.Frame,
            pitchFrame: jog is null ? null : PitchFrame(jog.Frame, texts, units),
            masterButtonFrame: master.Frame,
            strip: strip,
            overviewFrame: Overview(master.Frame, elements, units),
            barsText: null,
            elapsedSeconds: elapsed,
            remainingSeconds: remaining);
    }

    private static ScreenRect? Overview(ScreenRect master, List<UiaElementSnapshot> elements, Units units) =>
        elements.FirstOrDefault(element => element.ControlType == UiaControlType.Custom && element.Frame.Width >= units[300]
            && element.Frame.Height >= units[20] && element.Frame.Height <= units[50]
            && element.Frame.MinY >= master.MaxY && element.Frame.MinY <= master.MaxY + units[30])?.Frame;

    private static ScreenRect? PitchFrame(ScreenRect jog, List<UiaElementSnapshot> texts, Units units)
    {
        var near = texts.Where(text => Math.Abs(text.Frame.MinY - jog.MaxY) <= units[3] && text.Frame.Height <= units[16]
            && text.Frame.MinX >= jog.MinX - units[20] && text.Frame.MinX <= jog.MaxX + units[20]).ToList();
        var range = near.FirstOrDefault(text => HasHelpPrefix(text, "Tempo Range"))
            ?? near.FirstOrDefault(text => (text.Value ?? "").StartsWith('±'));
        if ((range ?? near.FirstOrDefault()) is not { } first) return null;
        var rowTop = first.Frame.MinY;
        var row = near.Where(text => Math.Abs(text.Frame.MinY - rowTop) <= units[1]);
        var pitch = row.Where(text => text != range && text.Frame.MaxX <= (range?.Frame.MinX ?? double.PositiveInfinity) + units[1]).ToList();
        if (pitch.Count == 0) return null;
        return pitch.Skip(1).Aggregate(pitch[0].Frame, (union, text) => union.Union(text.Frame));
    }

    private static double? Time(Regex pattern, List<UiaElementSnapshot> row, Units units)
    {
        var main = row.FirstOrDefault(text => pattern.IsMatch(text.Value ?? ""));
        if (main?.Value is not { } value) return null;
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length != 4) return null;
        var minutes = int.Parse(digits[..2]);
        var seconds = int.Parse(digits[2..]);
        var tenthsText = row.FirstOrDefault(text => Math.Abs(text.Frame.MinX - main.Frame.MaxX) <= units[2] && TenthsPattern().IsMatch(text.Value ?? ""));
        var tenths = tenthsText?.Value is { } tenthsValue ? int.Parse(tenthsValue[1..]) : 0;
        return (minutes * 600 + seconds * 10 + tenths) / 10.0;
    }

    private static bool HasHelpPrefix(UiaElementSnapshot element, string prefix) =>
        element.HelpText?.StartsWith(prefix, StringComparison.Ordinal) == true;

    [GeneratedRegex(@"^[0-9]{2,3}\.[0-9]{2}$")]
    private static partial Regex HeaderBPMPattern();

    [GeneratedRegex(@"^[0-9]{2}:[0-9]{2}$")]
    private static partial Regex ElapsedPattern();

    [GeneratedRegex(@"^-[0-9]{2}:[0-9]{2}$")]
    private static partial Regex RemainingPattern();

    [GeneratedRegex(@"^\.[0-9]$")]
    private static partial Regex TenthsPattern();
}
