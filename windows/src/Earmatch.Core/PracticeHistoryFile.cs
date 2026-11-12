using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using IOPath = System.IO.Path;

namespace Earmatch.Core;

/// What loading the practice history found: the history, and for a file that could not be used, why (Problem) and
/// where it was kept (QuarantinedPath), so the next save does not overwrite it. IsFileInTheWay says the unusable file
/// is still at the history's path (it could not be read, or it is corrupt and could not be moved aside): saving would
/// overwrite it, so nothing must be saved.
public sealed record LoadedPracticeHistory(PracticeHistory History, string? QuarantinedPath = null, string? Problem = null,
    bool IsFileInTheWay = false);

/// The practice history file, in the macOS app's format (PracticeHistoryFile.swift, Swift's JSONEncoder with sorted
/// keys): {"lastTrace", "mixes", "version": 1}, dates as seconds since 2001-01-01, upper-case UUIDs, trace samples as
/// "t" and "e", and no key for a missing value. A file that cannot be decoded, or comes from a newer version, is
/// renamed to "practice.corrupt-<UTC time>.json" and the history starts empty; one that cannot be renamed either is left
/// in place and reported as in the way (PracticeStore then saves nothing over it). A file that cannot be read (another
/// program holds it, or it may not be opened) is not known to be corrupt: it is left in place, reported as unreadable
/// and in the way, and the history starts empty.
public sealed class PracticeHistoryFile
{
    public const int FormatVersion = 1;

    private static readonly DateTimeOffset ReferenceDate = new(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// The dates a DateTimeOffset holds (years 1 to 9999), in seconds since the reference date.
    private static readonly double EarliestSeconds = (DateTimeOffset.MinValue - ReferenceDate).TotalSeconds + 1;
    private static readonly double LatestSeconds = (DateTimeOffset.MaxValue - ReferenceDate).TotalSeconds - 1;

    public PracticeHistoryFile(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public LoadedPracticeHistory Load(DateTimeOffset? now = null)
    {
        if (!File.Exists(Path)) return new LoadedPracticeHistory(new PracticeHistory());
        byte[] data;
        try
        {
            data = File.ReadAllBytes(Path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new LoadedPracticeHistory(new PracticeHistory(), Problem: $"unreadable: {error.Message}", IsFileInTheWay: true);
        }
        try
        {
            return new LoadedPracticeHistory(Decode(data));
        }
        catch (InvalidDataException error)
        {
            return KeepAside(error.Message, now);
        }
    }

    /// Renames a corrupt file so the fresh history saved next does not overwrite it.
    private LoadedPracticeHistory KeepAside(string problem, DateTimeOffset? now)
    {
        try
        {
            var kept = QuarantinePath(now ?? DateTimeOffset.UtcNow);
            File.Move(Path, kept);
            return new LoadedPracticeHistory(new PracticeHistory(), kept, problem);
        }
        catch (Exception moveError) when (moveError is IOException or UnauthorizedAccessException)
        {
            return new LoadedPracticeHistory(new PracticeHistory(), Problem: $"{problem}; not renamed: {moveError.Message}", IsFileInTheWay: true);
        }
    }

    /// Writes the history through a temporary file in the same folder, creating the folder when needed.
    public void Save(PracticeHistory history)
    {
        var directory = IOPath.GetDirectoryName(IOPath.GetFullPath(Path))!;
        Directory.CreateDirectory(directory);
        var temporary = IOPath.Combine(directory, $".{IOPath.GetFileName(Path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, Encode(history));
            File.Move(temporary, Path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static string Encode(PracticeHistory history)
    {
        var envelope = Sorted(
            ("lastTrace", history.LastTrace is { } trace ? Encode(trace) : null),
            ("mixes", new JsonArray(history.Mixes.Select(mix => (JsonNode?)Encode(mix)).ToArray())),
            ("version", FormatVersion));
        return envelope.ToJsonString();
    }

    private static JsonObject Encode(PracticeMix mix) => Sorted(
        ("bothPlayingSeconds", mix.BothPlayingSeconds),
        ("endedAt", Seconds(mix.EndedAt)),
        ("id", Uuid(mix.Id)),
        ("incomingDeck", mix.IncomingDeck),
        ("meanPhaseErrorMilliseconds", mix.MeanPhaseErrorMilliseconds),
        ("outgoingDeck", mix.OutgoingDeck),
        ("secondsToMatch", mix.SecondsToMatch),
        ("startedAt", Seconds(mix.StartedAt)),
        ("tempoDriftBPM", mix.TempoDriftBPM),
        ("worstPhaseErrorMilliseconds", mix.WorstPhaseErrorMilliseconds));

    private static JsonObject Encode(PracticeTrace trace) => Sorted(
        ("durationSeconds", trace.DurationSeconds),
        ("matchedAtSeconds", trace.MatchedAtSeconds),
        ("mixID", Uuid(trace.MixID)),
        ("samples", new JsonArray(trace.Samples.Select(sample => (JsonNode?)Sorted(("e", sample.ErrorMilliseconds), ("t", sample.Seconds))).ToArray())));

    /// An object with the keys in ordinal order and no key for a missing value, like Swift's sorted-keys encoding.
    private static JsonObject Sorted(params (string Key, JsonNode? Value)[] entries)
    {
        var result = new JsonObject();
        foreach (var (key, value) in entries.Where(entry => entry.Value is not null).OrderBy(entry => entry.Key, StringComparer.Ordinal))
            result[key] = value;
        return result;
    }

    private static double Seconds(DateTimeOffset date) => (date - ReferenceDate).Ticks / (double)TimeSpan.TicksPerSecond;

    private static string Uuid(Guid id) => id.ToString("D").ToUpperInvariant();

    internal static PracticeHistory Decode(byte[] data)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(data);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException(error.Message, error);
        }
        if (root is not JsonObject envelope) throw new InvalidDataException("the practice history is not a JSON object");
        var version = Integer(envelope, "version");
        if (version != FormatVersion) throw new InvalidDataException($"unsupported practice history version {version}");
        var mixes = Objects(envelope, "mixes").Select(DecodeMix).ToList();
        var lastTrace = envelope["lastTrace"] switch
        {
            null => null,
            JsonObject trace => DecodeTrace(trace),
            _ => throw new InvalidDataException("\"lastTrace\" is not an object"),
        };
        return new PracticeHistory(mixes, lastTrace);
    }

    private static PracticeMix DecodeMix(JsonObject mix) => new(
        UuidValue(mix, "id"),
        Date(mix, "startedAt"),
        Date(mix, "endedAt"),
        Integer(mix, "outgoingDeck"),
        Integer(mix, "incomingDeck"),
        Number(mix, "bothPlayingSeconds"),
        OptionalNumber(mix, "secondsToMatch"),
        OptionalNumber(mix, "tempoDriftBPM"),
        OptionalNumber(mix, "meanPhaseErrorMilliseconds"),
        OptionalNumber(mix, "worstPhaseErrorMilliseconds"));

    private static PracticeTrace DecodeTrace(JsonObject trace) => new(
        UuidValue(trace, "mixID"),
        Number(trace, "durationSeconds"),
        OptionalNumber(trace, "matchedAtSeconds"),
        Objects(trace, "samples").Select(sample => new PracticeTraceSample(Number(sample, "t"), Number(sample, "e"))).ToValueList());

    private static IEnumerable<JsonObject> Objects(JsonObject parent, string key) =>
        parent[key] is JsonArray array
            ? array.Select(item => item as JsonObject ?? throw new InvalidDataException($"an item of \"{key}\" is not an object")).ToList()
            : throw new InvalidDataException($"\"{key}\" is missing or not an array");

    private static double Number(JsonObject parent, string key) =>
        OptionalNumber(parent, key) ?? throw new InvalidDataException($"\"{key}\" is missing");

    private static double? OptionalNumber(JsonObject parent, string key) => parent[key] switch
    {
        null => null,
        JsonValue value when value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<double>(out var number) => number,
        _ => throw new InvalidDataException($"\"{key}\" is not a number"),
    };

    private static int Integer(JsonObject parent, string key) =>
        parent[key] is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<int>(out var number)
            ? number
            : throw new InvalidDataException($"\"{key}\" is missing or not an integer");

    private static Guid UuidValue(JsonObject parent, string key) =>
        parent[key] is JsonValue value && value.TryGetValue<string>(out var text) && Guid.TryParseExact(text, "D", out var id)
            ? id
            : throw new InvalidDataException($"\"{key}\" is missing or not a UUID");

    private static DateTimeOffset Date(JsonObject parent, string key)
    {
        var seconds = Number(parent, key);
        if (!(seconds >= EarliestSeconds && seconds <= LatestSeconds)) throw new InvalidDataException($"\"{key}\" is out of range");
        return ReferenceDate.AddTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond));
    }

    private string QuarantinePath(DateTimeOffset date)
    {
        var directory = IOPath.GetDirectoryName(IOPath.GetFullPath(Path))!;
        var stem = $"{IOPath.GetFileNameWithoutExtension(Path)}.corrupt-{date.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}";
        var extension = IOPath.GetExtension(Path);
        var candidate = IOPath.Combine(directory, stem + extension);
        for (var counter = 2; File.Exists(candidate); counter++) candidate = IOPath.Combine(directory, $"{stem}-{counter}{extension}");
        return candidate;
    }
}
