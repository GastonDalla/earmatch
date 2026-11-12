using System.Diagnostics;
using Earmatch.Core;

namespace Earmatch.App.Rekordbox;

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

    public string? ProductVersion { get; }

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
