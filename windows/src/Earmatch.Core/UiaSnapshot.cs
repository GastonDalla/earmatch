using System.Globalization;
using System.Text.Json;

namespace Earmatch.Core;

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
        public static UiaControlType FromProgrammaticName(string? name)
        {
            var bare = name?.StartsWith("ControlType.", StringComparison.Ordinal) == true ? name["ControlType.".Length..] : name;
            return Enum.TryParse<UiaControlType>(bare, ignoreCase: false, out var type) && type != UiaControlType.Other ? type : UiaControlType.Other;
        }
    }
}

public sealed record UiaElementSnapshot(UiaControlType ControlType, ScreenRect Frame, string? Name = null, string? Value = null, string? HelpText = null);

public sealed record RekordboxWindowSnapshot(ScreenRect WindowFrame, IReadOnlyList<UiaElementSnapshot> Elements, double Scale = 1);

public sealed record UiaNode(int Index, int Parent, int Depth, string? ControlType, string? Name, string? HelpText, string? Value, ScreenRect? Frame)
{
    public static string? FirstLine(string? help)
    {
        if (help is null) return null;
        var end = help.IndexOfAny(['\r', '\n']);
        return (end < 0 ? help : help[..end]).TrimEnd();
    }
}

public sealed record UiaDumpInfo(DateTimeOffset CapturedAt, string Os, string Arch, double? Scale, int? ProcessId, string? ProcessPath,
    string? ProcessVersion);

public static class UiaDump
{
    public static RekordboxWindowSnapshot Parse(string json, double scale) => Snapshot(ReadNodes(json), scale);

    public static RekordboxWindowSnapshot Snapshot(IReadOnlyList<UiaNode> nodes, double scale) => Snapshot(nodes, scale, out _);

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
