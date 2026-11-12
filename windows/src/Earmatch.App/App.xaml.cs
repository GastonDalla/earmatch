using System.Windows;
using System.Windows.Threading;
using Earmatch.App.Capture;
using Earmatch.App.Dialogs;
using Earmatch.App.Hotkeys;
using Earmatch.App.Overlay;
using Earmatch.App.Panes;
using Earmatch.App.Practice;
using Earmatch.App.Rekordbox;
using Earmatch.App.Tray;
using Earmatch.App.Updates;
using Earmatch.Core;
using Microsoft.Win32;
using Velopack;

namespace Earmatch.App;

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
    private AppUpdater? _updater;
    private MainWindowPlacement? _placement;
    private AboutWindow? _about;
    private DispatcherTimer? _liveExpiry;
    private double _liveReceivedAt;
    private bool _isPeeking;

    private const double LiveSnapshotLifetime = 0.75;

    private static readonly TimeSpan PracticeFlushTimeout = TimeSpan.FromSeconds(2);

    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

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
        _placement = new MainWindowPlacement(_window, store);
        _tray = new TrayIcon(_model, ShowWindow, ShowAbout, CopyDiagnostics, Quit);
        _updater = new AppUpdater(store, IsRekordboxFrontmost, () => IsWindowShown ? _window : null, Quit);
        _updater.Changed += () => _model.ApplyUpdates(_updater.State);
        _model.ApplyUpdates(_updater.State);
        _model.CheckForUpdates = _updater.ChooseMenuItem;

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
        _placement.Restore();
        ShowWindow();
        _updater.Start();
    }

    private static bool IsRekordboxFrontmost()
    {
        var processId = NativeMethods.ForegroundProcessId();
        if (processId == 0) return false;
        var path = NativeMethods.ProcessImagePath((int)processId);
        return path is not null && string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), RekordboxProduct.ProcessName, StringComparison.OrdinalIgnoreCase);
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess()) Render();
        else Dispatcher.BeginInvoke(Render);
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e) => _hotkeys?.Reset();

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

    private void ShowAbout()
    {
        if (_about is null)
        {
            _about = new AboutWindow(IsWindowShown ? _window : null);
            _about.Closed += (_, _) => _about = null;
        }
        _about.ShowAndActivate();
    }

    private bool CopyDiagnostics()
    {
        if (_model is null) return false;
        var version = SystemInfo.ShortVersion ?? L10n.English(() => WindowText.DevelopmentBuild);
        var report = new DiagnosticsReport(SystemInfo.VersionWithBuild ?? version, SystemInfo.WindowsVersion, SystemInfo.Architecture,
            _model.RekordboxVersion, _model.Activation, _model.Settings, LayoutDiagnostics(version))
        {
            Updates = _model.Updates.DiagnosticsLine,
            Model = SystemInfo.HardwareModel,
        };
        return ClipboardRetry.TryWrite(() => Clipboard.SetText(report.Text), () => Thread.Sleep(50));
    }

    private string LayoutDiagnostics(string version)
    {
        if (_overlays is null) return "";
        var text = _overlays.Diagnostics(version, SystemInfo.WindowsVersion).Text;
        return _hotkeys?.Problem is { } problem ? $"{text}\nHotkeys: {problem}" : text;
    }

    private void Quit()
    {
        _placement?.Save();
        if (_window is not null) _window.IsQuitting = true;
        StopMonitoring();
        _tray?.Dispose();
        _tray = null;
        Shutdown();
    }

    private void StopMonitoring()
    {
        _updater?.Dispose();
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
            try
            {
                LetRunningInstanceComeToFront();
            }
            finally
            {
                signal.Set();
            }
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void LetRunningInstanceComeToFront()
    {
        using var current = System.Diagnostics.Process.GetCurrentProcess();
        foreach (var process in ProcessesNamedLike(current))
        {
            using (process)
            {
                if (process.Id != current.Id && IsInSession(process, current.SessionId)) NativeMethods.AllowForeground(process.Id);
            }
        }
    }

    private static System.Diagnostics.Process[] ProcessesNamedLike(System.Diagnostics.Process current)
    {
        try
        {
            return System.Diagnostics.Process.GetProcessesByName(current.ProcessName);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return [];
        }
    }

    private static bool IsInSession(System.Diagnostics.Process process, int sessionId)
    {
        try
        {
            return process.SessionId == sessionId;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StopMonitoring();
        if (_practice?.Flush(PracticeFlushTimeout) == false)
            System.Diagnostics.Trace.WriteLine("earmatch: the practice history was still being saved when Earmatch quit");
        _tray?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    private static void AttachParentConsole() => NativeMethods.AttachConsole(-1);
}
