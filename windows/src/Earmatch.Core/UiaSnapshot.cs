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

/// Reads the JSON of windows/tools/dump-uia.ps1 (the format of the uia-*.json fixtures).
public static class UiaDump
{
    /// The snapshot of the first top-level window named "rekordbox": its frame and every element under it. Empty names
    /// and help texts become null; elements without a frame are left out.
    public static RekordboxWindowSnapshot Parse(string json, double scale)
    {
        using var document = JsonDocument.Parse(json);
        var nodes = document.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        var window = nodes.FirstOrDefault(node => node.GetProperty("depth").GetInt32() == 0
            && node.GetProperty("controlType").GetString() == "ControlType.Window" && String(node, "name") == "rekordbox");
        if (window.ValueKind == JsonValueKind.Undefined) throw new FormatException("The dump has no rekordbox window.");
        var windowIndex = window.GetProperty("index").GetInt32();
        var inWindow = new HashSet<int> { windowIndex };
        var elements = new List<UiaElementSnapshot>();
        foreach (var node in nodes)
        {
            if (!inWindow.Contains(node.GetProperty("parent").GetInt32())) continue;
            inWindow.Add(node.GetProperty("index").GetInt32());
            if (Frame(node) is not { } frame) continue;
            elements.Add(new UiaElementSnapshot(
                UiaControlType.FromProgrammaticName(String(node, "controlType")),
                frame,
                Name: NonEmpty(String(node, "name")),
                Value: String(node, "value"),
                HelpText: NonEmpty(String(node, "helpText"))));
        }
        return new RekordboxWindowSnapshot(Frame(window) ?? default, elements, scale);
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
