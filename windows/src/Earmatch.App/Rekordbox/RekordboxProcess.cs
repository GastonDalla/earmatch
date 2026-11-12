using System.Diagnostics;
using Earmatch.Core;

namespace Earmatch.App.Rekordbox;

/// A running rekordbox for Windows (rekordbox.exe) and its version. It keeps the process open, so telling whether it
/// still runs costs no process enumeration.
internal sealed class RekordboxProcess : IDisposable
{
    private readonly Process _process;

    private RekordboxProcess(Process process, string? path)
    {
        _process = process;
        Id = process.Id;
        Path = path;
        ProductVersion = ProductVersionOf(path);
    }

    public int Id { get; }
    public string? Path { get; }

    /// The executable's product version ("7.2.19.0342").
    public string? ProductVersion { get; }

    /// The version as rekordbox shows it ("7.2.19").
    public string? Version => RekordboxProduct.ShortVersion(ProductVersion);

    public bool IsRunning => !HasExited(_process);

    public static RekordboxProcess? Find()
    {
        RekordboxProcess? found = null;
        foreach (var process in Process.GetProcessesByName(RekordboxProduct.ProcessName))
        {
            if (found is null && !HasExited(process)) found = new RekordboxProcess(process, NativeMethods.ProcessImagePath(process.Id));
            else process.Dispose();
        }
        return found;
    }

    /// rekordbox's main window: its largest visible, unowned top-level window titled "rekordbox" (its dialogs and the
    /// splash screen have other titles or an owner). Zero when it has none yet.
    public IntPtr MainWindow() =>
        NativeMethods.TopLevelWindows(Id)
            .Where(window => window.Title == MainWindowListCheck.MainWindowTitle)
            .Select(window => (window.Window, Area: NativeMethods.GetWindowRect(window.Window, out var rect) ? (long)rect.Width * rect.Height : 0))
            .OrderByDescending(window => window.Area)
            .Select(window => window.Window)
            .FirstOrDefault();

    public void Dispose() => _process.Dispose();

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // Windows did not let Earmatch open it: it runs, as another user or elevated.
            return false;
        }
    }

    private static string? ProductVersionOf(string? path)
    {
        if (path is null) return null;
        try
        {
            return FileVersionInfo.GetVersionInfo(path).ProductVersion;
        }
        catch (System.IO.FileNotFoundException)
        {
            return null;
        }
    }
}
