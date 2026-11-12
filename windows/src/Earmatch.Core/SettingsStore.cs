using System.Text.Json;
using System.Text.Json.Nodes;

namespace Earmatch.Core;

public delegate void SettingsChange(ref OverlaySettings settings);

/// The settings file: the overlay settings under "overlaySettings" (the macOS UserDefaults key and JSON) plus small
/// string values such as "appLanguage" and "mainWindowPane". A missing or damaged file reads as the defaults; every
/// change is written at once, through a temporary file, and announced through Changed.
public sealed class SettingsStore
{
    private const string SettingsKey = "overlaySettings";
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly string _path;
    private readonly JsonObject _document;
    private OverlaySettings _settings;

    public SettingsStore(string path)
    {
        _path = path;
        _document = Read(path);
        _settings = ReadSettings(_document[SettingsKey]);
    }

    public event EventHandler? Changed;

    public OverlaySettings Settings
    {
        get => _settings;
        set
        {
            if (value == _settings) return;
            _settings = value;
            _document[SettingsKey] = JsonSerializer.SerializeToNode(value);
            Save();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Update(SettingsChange change)
    {
        var copy = _settings;
        change(ref copy);
        Settings = copy;
    }

    public string? Value(string key) => _document[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    public void SetValue(string key, string? value)
    {
        if (Value(key) == value && (value is not null || !_document.ContainsKey(key))) return;
        if (value is null) _document.Remove(key);
        else _document[key] = value;
        Save();
    }

    public string? Value(string key, string member) =>
        _document[key] is JsonObject group && group[member] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    public void SetValue(string key, string member, string? value)
    {
        if (Value(key, member) == value) return;
        if (_document[key] is not JsonObject group)
        {
            if (value is null) return;
            group = [];
            _document[key] = group;
        }
        if (value is null) group.Remove(member);
        else group[member] = value;
        Save();
    }

    private static JsonObject Read(string path)
    {
        try
        {
            return File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject document ? document : [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static OverlaySettings ReadSettings(JsonNode? node)
    {
        try
        {
            return node is JsonObject ? node.Deserialize<OverlaySettings>() : new OverlaySettings();
        }
        catch (JsonException)
        {
            return new OverlaySettings();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, _document.ToJsonString(Indented));
        File.Move(temporary, _path, overwrite: true);
    }
}
