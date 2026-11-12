namespace Earmatch.Core;

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
