using System.Windows;

namespace Earmatch.App;

/// <summary>
/// Earmatch for Windows. The tray icon, overlays and main window are added task by task
/// (odd/tasks/windows-port.md); for now the app only starts and quits.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Shutdown();
    }
}
