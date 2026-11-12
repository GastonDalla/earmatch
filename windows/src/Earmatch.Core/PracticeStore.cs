namespace Earmatch.Core;

/// The practice history behind the Practice pane (PracticeStore.swift): loaded once from the history file, recorded
/// mix by mix and saved after each one, and summarized for the window. A file that could not be read and could not be
/// moved aside either is never overwritten: the history then lives in memory until Earmatch quits. Safe to call from
/// any thread; the app records off the window's thread.
public sealed class PracticeStore
{
    private readonly object _gate = new();
    private readonly PracticeHistoryFile _file;
    private readonly TimeZoneInfo _zone;
    private readonly Action<string>? _log;
    private PracticeHistory? _history;
    private bool _mayWrite = true;

    public PracticeStore(PracticeHistoryFile file, TimeZoneInfo zone, Action<string>? log = null)
    {
        _file = file;
        _zone = zone;
        _log = log;
    }

    /// Why the history file could not be used or saved, if it could not.
    public string? Problem
    {
        get
        {
            lock (_gate) return _problem;
        }
    }

    private string? _problem;

    public PracticeOverview Load()
    {
        lock (_gate) return PracticeOverview.From(Loaded(), _zone);
    }

    public PracticeOverview Record(PracticeResult result)
    {
        lock (_gate)
        {
            var history = Loaded();
            history.Record(result);
            if (_mayWrite)
            {
                try
                {
                    _file.Save(history);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    _problem = $"not saved: {error.Message}";
                    _log?.Invoke($"Practice history not saved: {error.Message}");
                }
            }
            return PracticeOverview.From(history, _zone);
        }
    }

    private PracticeHistory Loaded()
    {
        if (_history is { } history) return history;
        var loaded = _file.Load();
        if (loaded.Problem is { } problem)
        {
            _problem = problem;
            _log?.Invoke($"Practice history unreadable, starting fresh: {problem}");
        }
        if (loaded.QuarantinedPath is { } kept) _log?.Invoke($"Unreadable practice history kept as {Path.GetFileName(kept)}");
        if (loaded.IsFileInTheWay)
        {
            _mayWrite = false;
            _log?.Invoke("The unreadable practice history could not be moved aside; new mixes are kept until Earmatch quits");
        }
        _history = loaded.History;
        return loaded.History;
    }
}
