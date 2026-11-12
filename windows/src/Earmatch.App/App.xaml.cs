using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Earmatch.App.Panes;
using Earmatch.App.Tray;
using Earmatch.Core;

namespace Earmatch.App;

/// Earmatch for Windows: the tray icon and the main window. Overlays, rekordbox monitoring, capture and hotkeys come in
/// later tasks (odd/tasks/windows-port.md); until then the window waits for rekordbox.
public partial class App : Application
{
    private const string InstanceName = @"Local\Earmatch.SingleInstance";
    private const string ShowSignalName = @"Local\Earmatch.ShowWindow";

    private Mutex? _instance;
    private EventWaitHandle? _showSignal;
    private EarmatchWindow? _window;
    private TrayIcon? _tray;
    private MainWindowModel? _model;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        WindowRenderRequest? request;
        try
        {
            request = WindowRenderRequest.Parse(e.Args);
        }
        catch (WindowRenderProblem problem)
        {
            AttachParentConsole();
            Console.Error.WriteLine(problem.Message);
            Shutdown(64);
            return;
        }
        var store = new SettingsStore(AppPaths.SettingsFile);
        AppLanguage.Install(request is null ? AppLanguageChoice.FromStoredValue(store.Value(AppLanguageChoice.StorageKey)) : AppLanguageChoice.System);
        if (request is not null)
        {
            AttachParentConsole();
            Shutdown(WindowRenderCommand.Run(request));
            return;
        }
        if (!ClaimSingleInstance())
        {
            Shutdown();
            return;
        }
        Start(store);
    }

    private void Start(SettingsStore store)
    {
        _model = new MainWindowModel(store);
        _model.CopyDiagnostics = CopyDiagnostics;
        _model.Quit = Quit;
        _window = new EarmatchWindow(_model);
        MainWindow = _window;
        _tray = new TrayIcon(_model, ShowWindow, () =>
        {
            _model.Select(MainWindowPane.Setup);
            ShowWindow();
        }, CopyDiagnostics, Quit);
        ShowWindow();
    }

    private void ShowWindow() => _window?.ShowAndActivate();

    private void CopyDiagnostics()
    {
        if (_model is null) return;
        var report = new DiagnosticsReport(SystemInfo.ShortVersion ?? L10n.English(() => WindowText.DevelopmentBuild), SystemInfo.WindowsVersion,
            SystemInfo.Architecture, _model.RekordboxVersion, _model.Activation, _model.Settings, "");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetText(report.Text);
                return;
            }
            catch (COMException)
            {
                // Another app holds the clipboard; try again in a moment.
                Thread.Sleep(50);
            }
        }
    }

    private void Quit()
    {
        if (_window is not null) _window.IsQuitting = true;
        _tray?.Dispose();
        _tray = null;
        Shutdown();
    }

    /// One Earmatch at a time: a second launch shows the running one's window and exits.
    private bool ClaimSingleInstance()
    {
        _instance = new Mutex(initiallyOwned: true, InstanceName, out var isFirst);
        if (!isFirst)
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting(ShowSignalName);
                signal.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The running instance is still starting; it shows its window anyway.
            }
            _instance.Dispose();
            _instance = null;
            return false;
        }
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        var signalHandle = _showSignal;
        var listener = new Thread(() =>
        {
            while (signalHandle.WaitOne())
            {
                Dispatcher.BeginInvoke(ShowWindow, DispatcherPriority.Normal);
            }
        }) { IsBackground = true, Name = "Earmatch show signal" };
        listener.Start();
        return true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    private static void AttachParentConsole() => NativeMethods.AttachConsole(-1);
}
