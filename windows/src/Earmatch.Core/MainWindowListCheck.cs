namespace Earmatch.Core;

/// Decides from the titles of rekordbox's top-level windows whether its main window was closed. Platform-neutral: on
/// Windows the titles come from rekordbox's top-level windows (the main one is titled "rekordbox"); an empty list is
/// only believed when a second read confirms it, and a failed read (null) is unknown.
public sealed class MainWindowListCheck
{
    public const string MainWindowTitle = "rekordbox";

    private bool _sawEmptyList;

    public bool IsMainWindowGone(IReadOnlyCollection<string>? windowTitles)
    {
        if (windowTitles is null)
        {
            _sawEmptyList = false;
            return false;
        }
        if (windowTitles.Count == 0)
        {
            var confirmed = _sawEmptyList;
            _sawEmptyList = true;
            return confirmed;
        }
        _sawEmptyList = false;
        return !windowTitles.Contains(MainWindowTitle);
    }

    public void MainWindowFound() => _sawEmptyList = false;
}
