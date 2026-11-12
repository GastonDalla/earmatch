using System.Globalization;
using System.Text.Json;

namespace Earmatch.Core;

/// The UI Automation control types the rekordbox locator reads (rekordbox for Windows is a JUCE app).
public enum UiaControlType
{
    Other,
    Window,
    Button,
    ComboBox,
    Custom,
    Edit,
    Text,
    Image,
}

public static class UiaControlTypeMembers
{
    extension(UiaControlType)
    {
        /// The control type of a UI Automation programmatic name ("ControlType.Button"), Other when the locator ignores it.
        public static UiaControlType FromProgrammaticName(string? name)
        {
            var bare = name?.StartsWith("ControlType.", StringComparison.Ordinal) == true ? name["ControlType.".Length..] : name;
            return Enum.TryParse<UiaControlType>(bare, ignoreCase: false, out var type) && type != UiaControlType.Other ? type : UiaControlType.Other;
        }
    }
}

/// One UI Automation element of the rekordbox window: what the macOS AXElementSnapshot holds, with the Windows names
/// (Name is the AX title, HelpText the AX help). Frame is in physical screen pixels.
public sealed record UiaElementSnapshot(UiaControlType ControlType, ScreenRect Frame, string? Name = null, string? Value = null, string? HelpText = null);

/// The rekordbox main window and its elements, in tree order. Scale is the physical pixels per rekordbox layout unit
/// (the window's DPI / 96): the locator's distances are in layout units, like the macOS points.
public sealed record RekordboxWindowSnapshot(ScreenRect WindowFrame, IReadOnlyList<UiaElementSnapshot> Elements, double Scale = 1);

/// One node of a UI Automation dump, as windows/tools/dump-uia.ps1 -Fixture writes it: its position in the raw view
/// (Parent is the index of its parent, -1 for a top-level window), its control type's programmatic name
/// ("ControlType.Button"), name, first help line, value (null when it has no Value pattern) and frame in physical
/// screen pixels (null when it has none).
public sealed record UiaNode(int Index, int Parent, int Depth, string? ControlType, string? Name, string? HelpText, string? Value, ScreenRect? Frame)
{
    /// The first line of a help text without its trailing spaces: the title the locator matches ("Deck BPM display:").
    public static string? FirstLine(string? help)
    {
        if (help is null) return null;
        var end = help.IndexOfAny(['\r', '\n']);
        return (end < 0 ? help : help[..end]).TrimEnd();
    }
}

/// The header of a dump: when and where it was read, and from which rekordbox.
public sealed record UiaDumpInfo(DateTimeOffset CapturedAt, string Os, string Arch, double? Scale, int? ProcessId, string? ProcessPath,
    string? ProcessVersion);

/// The JSON of windows/tools/dump-uia.ps1 (the format of the uia-*.json fixtures), which `Earmatch --dump-uia` writes
/// too, and the rekordbox window snapshot of its nodes.
public static class UiaDump
{
    /// The snapshot of the first top-level window named "rekordbox": its frame and every element under it. Empty names
    /// and help texts become null; elements without a frame are left out.
    public static RekordboxWindowSnapshot Parse(string json, double scale) => Snapshot(ReadNodes(json), scale);

    public static RekordboxWindowSnapshot Snapshot(IReadOnlyList<UiaNode> nodes, double scale) => Snapshot(nodes, scale, out _);

    /// The snapshot of the nodes, and for each of its elements the position of its node in `nodes`.
    public static RekordboxWindowSnapshot Snapshot(IReadOnlyList<UiaNode> nodes, double scale, out IReadOnlyList<int> positions)
    {
        var window = nodes.FirstOrDefault(node => node.Depth == 0 && node.ControlType == "ControlType.Window" && node.Name == "rekordbox")
            ?? throw new FormatException("The dump has no rekordbox window.");
        var inWindow = new HashSet<int> { window.Index };
        var elements = new List<UiaElementSnapshot>();
        var kept = new List<int>();
        for (var position = 0; position < nodes.Count; position++)
        {
            var node = nodes[position];
            if (!inWindow.Contains(node.Parent)) continue;
            inWindow.Add(node.Index);
            if (node.Frame is not { } frame) continue;
            elements.Add(new UiaElementSnapshot(UiaControlType.FromProgrammaticName(node.ControlType), frame, Name: NonEmpty(node.Name),
                Value: node.Value, HelpText: NonEmpty(node.HelpText)));
            kept.Add(position);
        }
        positions = kept;
        return new RekordboxWindowSnapshot(window.Frame ?? default, elements, scale);
    }

    public static IReadOnlyList<UiaNode> ReadNodes(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("nodes").EnumerateArray()
            .Select(node => new UiaNode(node.GetProperty("index").GetInt32(), node.GetProperty("parent").GetInt32(), node.GetProperty("depth").GetInt32(),
                String(node, "controlType"), String(node, "name"), String(node, "helpText"), String(node, "value"), Frame(node)))
            .ToList();
    }

    /// The dump of the nodes, one node per line like the fixtures. Non-ASCII text is escaped, so the JSON reads the same
    /// whatever code page the console that shows it uses.
    public static string Write(UiaDumpInfo info, IReadOnlyList<UiaNode> nodes)
    {
        var header = Json(writer =>
        {
            writer.WriteString("capturedAt", info.CapturedAt.ToString("o", CultureInfo.InvariantCulture));
            writer.WriteString("os", info.Os);
            writer.WriteString("arch", info.Arch);
            if (info.Scale is { } scale) writer.WriteNumber("scale", Math.Round(scale, 4));
            else writer.WriteNull("scale");
            writer.WriteStartObject("process");
            writer.WriteString("path", info.ProcessPath);
            if (info.ProcessId is { } id) writer.WriteNumber("id", id);
            else writer.WriteNull("id");
            writer.WriteString("version", info.ProcessVersion);
            writer.WriteEndObject();
            writer.WriteBoolean("truncated", false);
        });
        var lines = nodes.Select(node => Json(writer =>
        {
            writer.WriteNumber("index", node.Index);
            writer.WriteNumber("parent", node.Parent);
            writer.WriteNumber("depth", node.Depth);
            writer.WriteString("controlType", node.ControlType);
            writer.WriteString("name", node.Name);
            writer.WriteString("helpText", node.HelpText);
            writer.WriteString("value", node.Value);
            if (node.Frame is { } frame)
            {
                writer.WriteStartArray("frame");
                foreach (var value in (double[])[frame.X, frame.Y, frame.Width, frame.Height]) writer.WriteNumberValue(Math.Round(value, 1));
                writer.WriteEndArray();
            }
            else
            {
                writer.WriteNull("frame");
            }
        }));
        return header[..^1] + ",\n \"nodes\": [\n  " + string.Join(",\n  ", lines) + "\n ]\n}\n";
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string? String(JsonElement node, string key) =>
        node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? NonEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;

    private static ScreenRect? Frame(JsonElement node)
    {
        if (!node.TryGetProperty("frame", out var frame) || frame.ValueKind != JsonValueKind.Array) return null;
        var values = frame.EnumerateArray().Select(value => value.GetDouble()).ToArray();
        return values.Length == 4 ? new ScreenRect(values[0], values[1], values[2], values[3]) : null;
    }
}
