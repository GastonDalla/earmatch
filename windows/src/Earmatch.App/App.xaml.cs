using System.Windows;
using System.Windows.Threading;
using Earmatch.App.Capture;
using Earmatch.App.Hotkeys;
using Earmatch.App.Overlay;
using Earmatch.App.Panes;
using Earmatch.App.Practice;
using Earmatch.App.Rekordbox;
using Earmatch.App.Tray;
using Earmatch.Core;
using Microsoft.Win32;

namespace Earmatch.App;

/// Earmatch for Windows (AppDelegate.swift): the tray icon, the main window, the rekordbox monitor, the overlays with
/// the window capture, the mix trainer and the hotkeys. Every change of rekordbox, of the settings or of the peek
/// renders the overlays, the window's status and the tray icon again; the overlays feed the live decks mirror and the
/// mix trainer.
public partial class App : Application
{
    private const string InstanceName = @"Local\Earmatch.SingleInstance";
    private const string ShowSignalName = @"Local\Earmatch.ShowWindow";

    private Mutex? _instance;
    private EventWaitHandle? _showSignal;
    private EarmatchWindow? _window;
    private TrayIcon? _tray;
    private MainWindowModel? _model;
    private SettingsStore? _store;
    private HookThread? _hookThread;
    private RekordboxMonitor? _monitor;
    private HotkeyMonitor? _hotkeys;
    private OverlayController? _overlays;
    private PracticeController? _practice;
    private DispatcherTimer? _liveExpiry;
    private double _liveReceivedAt;
    private bool _isPeeking;

    /// How long the live decks mirror keeps a snapshot without a newer one (MainWindowController.liveSnapshotLifetime).
    private const double LiveSnapshotLifetime = 0.75;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains(DumpUiaCommand.Flag))
        {
            AttachParentConsole();
            Shutdown(DumpUiaCommand.Run());
            return;
        }
        if (Array.IndexOf(e.Args, CaptureFrameCommand.Flag) is var captureFlag and >= 0)
        {
            AttachParentConsole();
            Shutdown(CaptureFrameCommand.Run(e.Args.ElementAtOrDefault(captureFlag + 1) ?? Environment.CurrentDirectory));
            return;
        }
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
        _store = store;
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

        _overlays = new OverlayController(Dispatcher);
        _overlays.CaptureStatusChanged += ShowCaptureProblem;
        _overlays.LiveUpdate += snapshot =>
        {
            UpdateLive(snapshot);
            _practice?.Ingest(snapshot, CaptureClock.Now);
        };
        _practice = new PracticeController(new PracticeStore(new PracticeHistoryFile(AppPaths.PracticeFile), TimeZoneInfo.Local,
            message => System.Diagnostics.Trace.WriteLine($"earmatch: {message}")), Dispatcher);
        _practice.Changed += () => _model.ApplyPractice(_practice.Overview, _practice.Live);
        _practice.LoadHistory();
        _model.LanguageChanged += ShowCaptureProblem;
        _hookThread = new HookThread("Earmatch hooks");
        _monitor = new RekordboxMonitor(Dispatcher, _hookThread);
        _monitor.Changed += _ => Render();
        _hotkeys = new HotkeyMonitor(_hookThread, Dispatcher);
        _hotkeys.PeekChanged += peeking =>
        {
            _isPeeking = peeking;
            Render();
        };
        _hotkeys.ToggleEnabled += () => store.Update((ref OverlaySettings settings) => settings.IsEnabled = !settings.IsEnabled);
        store.Changed += OnSettingsChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        _hotkeys.Start();
        _window.IsVisibleChanged += (_, _) => ClearLiveUnlessShown();
        _window.StateChanged += (_, _) => ClearLiveUnlessShown();
        _monitor.Start();
        Render();
        ShowWindow();
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess()) Render();
        else Dispatcher.BeginInvoke(Render);
    }

    /// A lock or a user switch can hide a held key's release from the hook: the peek ends.
    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e) => _hotkeys?.Reset();

    /// The overlays, the window's status and live decks, and the tray icon for rekordbox's state and the settings.
    private void Render()
    {
        if (_store is null || _model is null || _monitor is null) return;
        var state = _monitor.State;
        var settings = _store.Settings;
        _overlays?.Update(state, settings, _isPeeking);
        if (!state.Activation.IsActive) ClearLive();
        _model.Apply(state.Activation, state.Version, state.Layout?.LayoutName);
        _tray?.SetStatus(state.Activation.IsActive && settings.IsEnabled, state.Activation.Message);
    }

    private void ShowCaptureProblem() => _model?.ApplyCaptureProblem(_overlays?.CaptureMessage);

    private bool IsWindowShown => _window is { IsVisible: true } window && window.WindowState != WindowState.Minimized;

    /// The live decks mirror (MainWindowController.updateLive): shown while the window is, and cleared when no newer
    /// snapshot came for a moment (the overlays stopped sending).
    private void UpdateLive(LiveDecksSnapshot snapshot)
    {
        if (_model is null || !IsWindowShown) return;
        _liveReceivedAt = CaptureClock.Now;
        _model.ApplyLive(snapshot);
        if (_liveExpiry is not null) return;
        _liveExpiry = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromSeconds(LiveSnapshotLifetime) };
        _liveExpiry.Tick += (_, _) =>
        {
            var remaining = _liveReceivedAt + LiveSnapshotLifetime - CaptureClock.Now;
            if (remaining > 0.001)
            {
                _liveExpiry!.Interval = TimeSpan.FromSeconds(remaining);
                return;
            }
            ClearLive();
        };
        _liveExpiry.Start();
    }

    private void ClearLive()
    {
        _liveExpiry?.Stop();
        _liveExpiry = null;
        _model?.ApplyLive(LiveDecksSnapshot.Empty);
    }

    private void ClearLiveUnlessShown()
    {
        if (!IsWindowShown) ClearLive();
    }

    private void ShowWindow() => _window?.ShowAndActivate();

    /// Copies the diagnostics report; false when the clipboard stayed busy (another app holds it open).
    private bool CopyDiagnostics()
    {
        if (_model is null) return false;
        var version = SystemInfo.ShortVersion ?? L10n.English(() => WindowText.DevelopmentBuild);
        var report = new DiagnosticsReport(version, SystemInfo.WindowsVersion, SystemInfo.Architecture, _model.RekordboxVersion, _model.Activation,
            _model.Settings, LayoutDiagnostics(version));
        return ClipboardRetry.TryWrite(() => Clipboard.SetText(report.Text), () => Thread.Sleep(50));
    }

    /// The capture section of the report (OverlayController.diagnostics on macOS): what UI Automation located and how
    /// the capture keeps up, with the hotkey hook's state.
    private string LayoutDiagnostics(string version)
    {
        if (_overlays is null) return "";
        var text = _overlays.Diagnostics(version, SystemInfo.WindowsVersion).Text;
        return _hotkeys?.Problem is { } problem ? $"{text}\nHotkeys: {problem}" : text;
    }

    private void Quit()
    {
        if (_window is not null) _window.IsQuitting = true;
        StopMonitoring();
        _tray?.Dispose();
        _tray = null;
        Shutdown();
    }

    private void StopMonitoring()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        if (_store is not null) _store.Changed -= OnSettingsChanged;
        _monitor?.Dispose();
        _monitor = null;
        _hotkeys?.Dispose();
        _hotkeys = null;
        _hookThread?.Dispose();
        _hookThread = null;
        _overlays?.Dispose();
        _overlays = null;
        ClearLive();
    }

    /// One Earmatch at a time: a second launch shows the running one's window and exits. An Earmatch running as
    /// administrator keeps a normal launch from opening its mutex and its signal; the normal launch then just exits.
    private bool ClaimSingleInstance()
    {
        bool isFirst;
        try
        {
            _instance = new Mutex(initiallyOwned: true, InstanceName, out isFirst);
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        if (!isFirst)
        {
            SignalRunningInstance();
            _instance.Dispose();
            _instance = null;
            return false;
        }
        try
        {
            _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        }
        catch (UnauthorizedAccessException)
        {
            // Another Earmatch as administrator still holds the signal: this one runs, and a later launch cannot raise it.
            return true;
        }
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

    private static void SignalRunningInstance()
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
        catch (UnauthorizedAccessException)
        {
            // The running instance is elevated: Windows does not let this launch signal it.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StopMonitoring();
        _tray?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    private static void AttachParentConsole() => NativeMethods.AttachConsole(-1);
}
