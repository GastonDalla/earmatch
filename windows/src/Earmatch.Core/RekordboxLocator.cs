using System.Text.RegularExpressions;

namespace Earmatch.Core;

internal static partial class RekordboxLocator
{
    private const double DeckHeaderHeight = 40;
    private const string ModeHelpPrefix = "Change EXPORT/PERFORMANCE/EDIT mode";
    private const string ZoomResetButtonInPlaceOfMacCaptureAndMatching = "RST";
    private const string DeckMasterHelpPrefixUnlikeTrafficLightMaster = "Sync Master";
    private const string JogBPMHelpPrefix = "Deck BPM display";
    private const string TempoRangeHelpPrefix = "Tempo Range";

    public static RekordboxLayout Locate(RekordboxWindowSnapshot snapshot)
    {
        var points = new MacPoints(snapshot.Scale);
        var elements = snapshot.Elements;
        var modeBox = elements.FirstOrDefault(element => element.ControlType == UiaControlType.ComboBox
            && HasHelpPrefix(element, ModeHelpPrefix));
        var mode = modeBox is null ? RekordboxMode.Unknown : Mode(modeBox.Value);
        var layoutName = modeBox is null ? null : LayoutBox(modeBox, elements, points)?.Value;
        var waveform = DeckArrangement.FromRawValue(layoutName) is { } arrangement ? MainWaveform(elements, arrangement, points) : null;
        var decks = LocateDecks(elements, snapshot.WindowFrame, waveform?.Strips ?? [], points);
        foreach (var deck in decks)
        {
            if (deck.Strip is { Orientation: StripOrientation.Vertical } strip) deck.BarsText = BarsText(strip.Frame, elements, points);
        }
        return new RekordboxLayout(mode, layoutName, waveform?.Stacked, decks, snapshot.Scale);
    }

    private readonly record struct MacPoints(double PixelsPerPoint)
    {
        public double this[double distance] => distance * PixelsPerPoint;
    }

    private sealed record Waveform(ScreenRect Stacked, Dictionary<int, WaveformStrip> Strips);

    private static RekordboxMode Mode(string? value) => value?.ToLowerInvariant() switch
    {
        "performance" => RekordboxMode.Performance,
        "export" => RekordboxMode.Export,
        "edit" => RekordboxMode.Edit,
        _ => RekordboxMode.Unknown,
    };

    private static UiaElementSnapshot? LayoutBox(UiaElementSnapshot mode, IReadOnlyList<UiaElementSnapshot> elements, MacPoints points) =>
        elements
            .Where(element => element.ControlType == UiaControlType.ComboBox && Math.Abs(element.Frame.MidY - mode.Frame.MidY) <= points[8]
                && element.Frame.MinX >= mode.Frame.MaxX)
            .MinBy(element => element.Frame.MinX);

    private static Waveform? MainWaveform(IReadOnlyList<UiaElementSnapshot> elements, DeckArrangement arrangement, MacPoints points)
    {
        var anchor = elements.FirstOrDefault(element => element.ControlType == UiaControlType.Button
            && element.Name == ZoomResetButtonInPlaceOfMacCaptureAndMatching);
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
            if (holder.Width >= points[80]) return null;
            var sameRows = groups.Where(group => Math.Abs(group.MinY - holder.MinY) <= points[1] && Math.Abs(group.Height - holder.Height) <= points[1]).ToList();
            if (sameRows.Count == 0) return null;
            stacked = sameRows.MaxBy(group => group.Width);
            frames = Distinct(groups.Where(group => group.MinY >= stacked.MinY - points[1] && group.MaxY <= stacked.MaxY + points[1]
                && group.Width >= stacked.Width - points[30] && Share(group.Height, stacked.Height)), frame => frame.MinY, points);
        }
        else
        {
            List<ScreenRect> Columns(ScreenRect candidate) =>
                Distinct(groups.Where(group => Math.Abs(group.MinY - candidate.MinY) <= points[1] && Math.Abs(group.Height - candidate.Height) <= points[1]
                    && group.MinX >= candidate.MinX - points[1] && group.MaxX <= candidate.MaxX + points[1] && Share(group.Width, candidate.Width)),
                    frame => frame.MinX, points);
            var found = groups
                .Where(group => Math.Abs(group.MinX - holder.MinX) <= points[1] && Math.Abs(group.Width - holder.Width) <= points[1]
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

    private static List<ScreenRect> Distinct(IEnumerable<ScreenRect> frames, Func<ScreenRect, double> key, MacPoints points)
    {
        var result = new List<ScreenRect>();
        foreach (var frame in frames)
        {
            if (!result.Any(kept => Math.Abs(key(kept) - key(frame)) <= points[1])) result.Add(frame);
        }
        return result.OrderBy(key).ToList();
    }

    private static string? BarsText(ScreenRect strip, IReadOnlyList<UiaElementSnapshot> elements, MacPoints points)
    {
        var text = elements.FirstOrDefault(element => element.ControlType == UiaControlType.Text && Math.Abs(element.Frame.MinY - strip.MaxY) <= points[6]
            && element.Frame.MidX >= strip.MinX && element.Frame.MidX < strip.MaxX);
        var value = Text.TrimWhitespaces(text?.Value ?? "");
        return value.Length == 0 ? null : value;
    }

    private static List<DeckLayout> LocateDecks(IReadOnlyList<UiaElementSnapshot> elements, ScreenRect window,
        Dictionary<int, WaveformStrip> strips, MacPoints points)
    {
        var masters = elements
            .Where(element => element.ControlType == UiaControlType.Button && element.Name == "MASTER"
                && HasHelpPrefix(element, DeckMasterHelpPrefixUnlikeTrafficLightMaster))
            .OrderBy(element => element.Frame.MinY)
            .ToList();
        if (masters.Count is not (2 or 4)) return [];
        var rows = masters.Chunk(2).Select(row => row.OrderBy(element => element.Frame.MinX).ToList()).ToList();
        if (!rows.All(row => Math.Abs(row[0].Frame.MinY - row[1].Frame.MinY) <= points[6])) return [];
        var halfWidth = window.Width / 2;
        var decks = new List<DeckLayout>();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var top = rowIndex == 0 ? double.NegativeInfinity : row[0].Frame.MinY - points[DeckHeaderHeight];
            var bottom = rowIndex + 1 < rows.Count ? rows[rowIndex + 1][0].Frame.MinY - points[DeckHeaderHeight] : double.PositiveInfinity;
            for (var column = 0; column < row.Count; column++)
            {
                var number = rowIndex * 2 + column + 1;
                var left = window.MinX + column * halfWidth;
                var inDeck = elements.Where(element => element.Frame.MidX >= left && element.Frame.MidX < left + halfWidth
                    && element.Frame.MidY >= top && element.Frame.MidY < bottom).ToList();
                decks.Add(Deck(number, row[column], inDeck, strips.GetValueOrDefault(number), points));
            }
        }
        return decks;
    }

    private static DeckLayout Deck(int number, UiaElementSnapshot master, List<UiaElementSnapshot> elements, WaveformStrip? strip, MacPoints points)
    {
        var texts = elements.Where(element => element.ControlType == UiaControlType.Text).ToList();
        var headerRow = texts.Where(text => Math.Abs(text.Frame.MinY - master.Frame.MinY) <= points[6] && text.Frame.Height <= points[18]).ToList();
        var header = headerRow.FirstOrDefault(text => string.IsNullOrEmpty(text.HelpText) && HeaderBPMPattern().IsMatch(text.Value ?? ""));
        var elapsed = Time(ElapsedPattern(), headerRow, points);
        var remaining = Time(RemainingPattern(), headerRow, points);
        var isLoaded = header is not null || (elapsed ?? 0) + (remaining ?? 0) > 0;
        var title = !isLoaded ? null : texts
            .Where(text => text.Frame.MaxY <= master.Frame.MinY + points[1] && text.Frame.MinY >= master.Frame.MinY - points[DeckHeaderHeight]
                && !string.IsNullOrEmpty(text.Value))
            .MaxBy(text => text.Frame.Width)?
            .Value;
        var jog = elements.FirstOrDefault(element => element.ControlType is UiaControlType.Edit or UiaControlType.Text
            && HasHelpPrefix(element, JogBPMHelpPrefix));
        return new DeckLayout(
            number: number,
            title: title,
            bpm: header is null ? null : BPMDisplayFormatter.Parse(header.Value ?? ""),
            headerBPMFrame: header?.Frame,
            jogBPMFrame: jog?.Frame,
            pitchFrame: jog is null ? null : PitchFrame(jog.Frame, texts, points),
            masterButtonFrame: master.Frame,
            strip: strip,
            overviewFrame: Overview(master.Frame, elements, points),
            barsText: null,
            elapsedSeconds: elapsed,
            remainingSeconds: remaining);
    }

    private static ScreenRect? Overview(ScreenRect master, List<UiaElementSnapshot> elements, MacPoints points) =>
        elements.FirstOrDefault(element => element.ControlType == UiaControlType.Custom && element.Frame.Width >= points[300]
            && element.Frame.Height >= points[20] && element.Frame.Height <= points[50]
            && element.Frame.MinY >= master.MaxY && element.Frame.MinY <= master.MaxY + points[30])?.Frame;

    private static ScreenRect? PitchFrame(ScreenRect jog, List<UiaElementSnapshot> texts, MacPoints points)
    {
        var near = texts.Where(text => Math.Abs(text.Frame.MinY - jog.MaxY) <= points[3] && text.Frame.Height <= points[16]
            && text.Frame.MinX >= jog.MinX - points[20] && text.Frame.MinX <= jog.MaxX + points[20]).ToList();
        var range = near.FirstOrDefault(text => HasHelpPrefix(text, TempoRangeHelpPrefix))
            ?? near.FirstOrDefault(text => (text.Value ?? "").StartsWith('±'));
        if ((range ?? near.FirstOrDefault()) is not { } first) return null;
        var rowTop = first.Frame.MinY;
        var row = near.Where(text => Math.Abs(text.Frame.MinY - rowTop) <= points[1]);
        var pitch = row.Where(text => text != range && text.Frame.MaxX <= (range?.Frame.MinX ?? double.PositiveInfinity) + points[1]).ToList();
        if (pitch.Count == 0) return null;
        return pitch.Skip(1).Aggregate(pitch[0].Frame, (union, text) => union.Union(text.Frame));
    }

    private static double? Time(Regex pattern, List<UiaElementSnapshot> row, MacPoints points)
    {
        var main = row.FirstOrDefault(text => pattern.IsMatch(text.Value ?? ""));
        if (main?.Value is not { } value) return null;
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length != 4) return null;
        var minutes = int.Parse(digits[..2]);
        var seconds = int.Parse(digits[2..]);
        var tenthsText = row.FirstOrDefault(text => Math.Abs(text.Frame.MinX - main.Frame.MaxX) <= points[2] && TenthsPattern().IsMatch(text.Value ?? ""));
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
