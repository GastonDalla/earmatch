using System.Text;
using System.Text.Json.Nodes;
using Earmatch.Core;

namespace Earmatch.Core.Tests;

public sealed class PracticeHistoryFileTests : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), $"earmatch-practice-tests-{Guid.NewGuid()}");
    private readonly string _directory;
    private readonly PracticeHistoryFile _file;

    public PracticeHistoryFileTests()
    {
        _directory = Path.Combine(_scratch, "Earmatch");
        _file = new PracticeHistoryFile(Path.Combine(_directory, "practice.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_scratch)) Directory.Delete(_scratch, recursive: true);
    }

    static PracticeResult Result(DateTimeOffset end, double? error = 10)
    {
        var mix = new PracticeMix(Guid.NewGuid(), end.AddSeconds(-60), end, OutgoingDeck: 1, IncomingDeck: 2, BothPlayingSeconds: 60,
            SecondsToMatch: 5, TempoDriftBPM: 0.01, MeanPhaseErrorMilliseconds: error, WorstPhaseErrorMilliseconds: error * 2);
        return new PracticeResult(mix, new PracticeTrace(mix.Id, 60, 5, [new PracticeTraceSample(0, 4)]));
    }

    string[] Contents() => Directory.GetFiles(_directory).Select(Path.GetFileName).Order().ToArray()!;

    [Fact]
    public void AMissingFileLoadsAnEmptyHistoryAndCreatesNothing()
    {
        var loaded = _file.Load();

        Assert.Empty(loaded.History.Mixes);
        Assert.Null(loaded.History.LastTrace);
        Assert.Null(loaded.QuarantinedPath);
        Assert.Null(loaded.Problem);
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void ASavedHistoryLoadsBackUnchanged()
    {
        var history = new PracticeHistory();
        history.Record(Result(DateTimeOffset.FromUnixTimeSeconds(1_793_840_000)));
        history.Record(Result(DateTimeOffset.FromUnixTimeSeconds(1_793_840_600), error: null));

        _file.Save(history);
        var loaded = _file.Load();

        Assert.Equal(history.Mixes, loaded.History.Mixes);
        Assert.Equal(history.LastTrace, loaded.History.LastTrace);
        Assert.Null(loaded.QuarantinedPath);
        Assert.Equal(["practice.json"], Contents());
    }

    [Fact]
    public void ACorruptFileStartsFreshAndIsKeptRenamed()
    {
        Directory.CreateDirectory(_directory);
        var garbage = Encoding.UTF8.GetBytes("{\"version\":1,\"mixes\":[{\"broken\"");
        File.WriteAllBytes(_file.Path, garbage);

        var loaded = _file.Load();

        Assert.Empty(loaded.History.Mixes);
        Assert.NotNull(loaded.QuarantinedPath);
        Assert.NotNull(loaded.Problem);
        Assert.Equal(Path.GetFullPath(_directory), Path.GetDirectoryName(Path.GetFullPath(loaded.QuarantinedPath)));
        Assert.StartsWith("practice.corrupt-", Path.GetFileName(loaded.QuarantinedPath));
        Assert.Equal(garbage, File.ReadAllBytes(loaded.QuarantinedPath));
        Assert.False(File.Exists(_file.Path));

        _file.Save(new PracticeHistory());
        Assert.Equal(2, Contents().Length);
    }

    [Fact]
    public void AFileFromANewerFormatIsKeptRenamedNotOverwritten()
    {
        Directory.CreateDirectory(_directory);
        var future = Encoding.UTF8.GetBytes("{\"version\":99,\"mixes\":[]}");
        File.WriteAllBytes(_file.Path, future);

        var loaded = _file.Load();

        Assert.Empty(loaded.History.Mixes);
        Assert.NotNull(loaded.QuarantinedPath);
        Assert.Equal(future, File.ReadAllBytes(loaded.QuarantinedPath));
        Assert.Contains("unsupported practice history version 99", loaded.Problem);
    }

    [Fact]
    public void TheQuarantineNameCarriesTheUtcTimeAndNeverOverwritesAnEarlierOne()
    {
        Directory.CreateDirectory(_directory);
        var now = new DateTimeOffset(2026, 11, 12, 10, 11, 12, TimeSpan.FromHours(-3));

        File.WriteAllText(_file.Path, "not json");
        var first = _file.Load(now).QuarantinedPath;
        File.WriteAllText(_file.Path, "still not json");
        var second = _file.Load(now).QuarantinedPath;

        Assert.Equal("practice.corrupt-20261112T131112Z.json", Path.GetFileName(first));
        Assert.Equal("practice.corrupt-20261112T131112Z-2.json", Path.GetFileName(second));
        Assert.Equal("still not json", File.ReadAllText(second!));
    }

    /// The file keeps the macOS format (PracticeHistoryFile.swift, Swift's JSONEncoder with sorted keys): dates in
    /// seconds since 2001-01-01, upper-case UUIDs, trace samples as "t" and "e", and no keys for missing values.
    [Fact]
    public void TheFileKeepsTheMacOSFormat()
    {
        var id = Guid.Parse("E621E1F8-C36C-495A-93FC-0C247A3E6E5F");
        var reference = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var mix = new PracticeMix(id, reference.AddSeconds(800_000_000), reference.AddSeconds(800_000_060.5), 1, 2, 60.5, 5, null, 12.25, 30);
        var history = new PracticeHistory();
        history.Record(new PracticeResult(mix, new PracticeTrace(id, 60.5, 5, [new PracticeTraceSample(0.1, -4.25)])));

        _file.Save(history);

        Assert.Equal(
            """{"lastTrace":{"durationSeconds":60.5,"matchedAtSeconds":5,"mixID":"E621E1F8-C36C-495A-93FC-0C247A3E6E5F","samples":[{"e":-4.25,"t":0.1}]},"mixes":[{"bothPlayingSeconds":60.5,"endedAt":800000060.5,"id":"E621E1F8-C36C-495A-93FC-0C247A3E6E5F","incomingDeck":2,"meanPhaseErrorMilliseconds":12.25,"outgoingDeck":1,"secondsToMatch":5,"startedAt":800000000,"worstPhaseErrorMilliseconds":30}],"version":1}""",
            File.ReadAllText(_file.Path));
    }

    [Fact]
    public void AFileWrittenByTheMacOSAppLoads()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_file.Path,
            """{"mixes":[{"bothPlayingSeconds":61.2,"endedAt":800000061.2,"id":"e621e1f8-c36c-495a-93fc-0c247a3e6e5f","incomingDeck":1,"outgoingDeck":2,"startedAt":800000000}],"version":1}""");

        var loaded = _file.Load();

        var mix = Assert.Single(loaded.History.Mixes);
        Assert.Equal(Guid.Parse("E621E1F8-C36C-495A-93FC-0C247A3E6E5F"), mix.Id);
        Assert.Equal(new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(800_000_000), mix.StartedAt);
        Assert.Equal(61.2, mix.BothPlayingSeconds);
        Assert.Equal((2, 1), (mix.OutgoingDeck, mix.IncomingDeck));
        Assert.Null(mix.SecondsToMatch);
        Assert.Null(mix.TempoDriftBPM);
        Assert.Null(loaded.History.LastTrace);
        Assert.Null(loaded.QuarantinedPath);
    }

    [Theory]
    [InlineData("""{"version":1}""")]
    [InlineData("""{"version":1,"mixes":[{"id":"not a uuid","startedAt":0,"endedAt":1,"outgoingDeck":1,"incomingDeck":2,"bothPlayingSeconds":9}]}""")]
    [InlineData("""{"version":1,"mixes":[{"startedAt":0,"endedAt":1,"outgoingDeck":1,"incomingDeck":2,"bothPlayingSeconds":9}]}""")]
    [InlineData("""{"version":1,"mixes":[],"lastTrace":{"mixID":"E621E1F8-C36C-495A-93FC-0C247A3E6E5F","durationSeconds":"long","samples":[]}}""")]
    [InlineData("[1, 2]")]
    public void AFileMissingWhatTheMacOSAppRequiresIsKeptRenamed(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_file.Path, json);

        var loaded = _file.Load();

        Assert.Empty(loaded.History.Mixes);
        Assert.NotNull(loaded.QuarantinedPath);
        Assert.Equal(json, File.ReadAllText(loaded.QuarantinedPath));
    }

    [Fact]
    public void SavingWritesNoTemporaryFileNextToTheHistory()
    {
        _file.Save(new PracticeHistory());
        _file.Save(new PracticeHistory());

        Assert.Equal(["practice.json"], Contents());
        Assert.Equal("""{"mixes":[],"version":1}""", File.ReadAllText(_file.Path));
        Assert.NotNull(JsonNode.Parse(File.ReadAllText(_file.Path)));
    }

    /// A date the file cannot hold as a date (before year 1 or after 9999) is a corrupt file, kept renamed, not a crash.
    [Theory]
    [InlineData(-9e10)]
    [InlineData(3e11)]
    public void AFileWithADateOutOfRangeIsKeptRenamed(double seconds)
    {
        Directory.CreateDirectory(_directory);
        var json = $$"""{"mixes":[{"bothPlayingSeconds":60,"endedAt":{{seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"id":"8D3C1F86-0F8E-4B5C-9F59-2C1C1B6E3A10","incomingDeck":2,"outgoingDeck":1,"startedAt":800000000}],"version":1}""";
        File.WriteAllText(_file.Path, json);

        var loaded = _file.Load(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));

        Assert.Empty(loaded.History.Mixes);
        Assert.NotNull(loaded.QuarantinedPath);
        Assert.Equal(json, File.ReadAllText(loaded.QuarantinedPath));
        Assert.False(File.Exists(_file.Path));
    }

    /// Holds the history file so it cannot be moved or deleted while it can still be read: Windows refuses to rename a
    /// file another handle has open without delete sharing; elsewhere a folder without write permission does it.
    IDisposable KeepInPlace()
    {
        if (OperatingSystem.IsWindows()) return new FileStream(_file.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        File.SetUnixFileMode(_directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        return new Restore(() => File.SetUnixFileMode(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute));
    }

    sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }

    /// Opens the history file with no sharing, so reading it fails with a sharing violation (an I/O error, not corruption).
    FileStream Lock() => new(_file.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    byte[] SaveOneMix()
    {
        var history = new PracticeHistory();
        history.Record(Result(new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero)));
        _file.Save(history);
        return File.ReadAllBytes(_file.Path);
    }

    /// A file that cannot be read right now (another program holds it) may be perfectly good: it is left where it is,
    /// not renamed as corrupt, and reported in the way so nothing is saved over it.
    [Fact]
    public void AFileThatCannotBeReadIsLeftInPlace()
    {
        var original = SaveOneMix();

        LoadedPracticeHistory loaded;
        using (Lock()) loaded = _file.Load(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));

        Assert.Empty(loaded.History.Mixes);
        Assert.StartsWith("unreadable: ", loaded.Problem);
        Assert.Null(loaded.QuarantinedPath);
        Assert.True(loaded.IsFileInTheWay);
        Assert.Equal(["practice.json"], Contents());
        Assert.Equal(original, File.ReadAllBytes(_file.Path));
    }

    /// Once the store could not read the file, it keeps the mixes of this session in memory and never saves over the
    /// file, even after the file can be read again: the next launch still finds every earlier mix.
    [Fact]
    public void TheStoreNeverSavesOverAFileItCouldNotRead()
    {
        var original = SaveOneMix();
        var store = new PracticeStore(_file, TimeZoneInfo.Utc);
        using (Lock()) store.Load();

        var overview = store.Record(Result(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero)));

        Assert.Equal(1, overview.MixCount);
        Assert.StartsWith("unreadable: ", store.Problem);
        Assert.Equal(original, File.ReadAllBytes(_file.Path));
        Assert.Equal(["practice.json"], Contents());
        Assert.Equal(1, new PracticeStore(_file, TimeZoneInfo.Utc).Load().MixCount);
    }

    /// A corrupt file that cannot be moved aside is left in place and never overwritten, even once it could be moved.
    [Fact]
    public void ACorruptFileThatCannotBeMovedIsNeverOverwritten()
    {
        Directory.CreateDirectory(_directory);
        var garbage = Encoding.UTF8.GetBytes("{\"version\":1,\"mixes\":[{\"broken\"");
        File.WriteAllBytes(_file.Path, garbage);
        var store = new PracticeStore(_file, TimeZoneInfo.Utc);
        using (KeepInPlace()) store.Load();

        var overview = store.Record(Result(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero)));

        Assert.Equal(1, overview.MixCount);
        Assert.Contains("not renamed: ", store.Problem);
        Assert.Equal(garbage, File.ReadAllBytes(_file.Path));
        Assert.Equal(["practice.json"], Contents());
    }

    /// The store saves each recorded mix and reports the history as the window shows it.
    [Fact]
    public void TheStoreSavesEveryRecordedMix()
    {
        var end = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
        var store = new PracticeStore(_file, TimeZoneInfo.Utc);

        Assert.Equal(0, store.Load().MixCount);
        var overview = store.Record(Result(end));

        Assert.Equal(1, overview.MixCount);
        Assert.Null(store.Problem);
        Assert.Equal(1, new PracticeStore(_file, TimeZoneInfo.Utc).Load().MixCount);
    }
}
