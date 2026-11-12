using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Threading;
using Earmatch.Core;

namespace Earmatch.App.Rekordbox;

/// rekordbox as the overlays and the window see it (RekordboxState in RekordboxMonitor.swift). The layout's frames are
/// physical screen pixels as they were at the last full read of the window, whose window rectangle was ReadRect; Offset
/// is how far the window moved since, and Scale its physical pixels per layout unit (DPI / 96).
internal sealed record RekordboxState(OverlayActivation Activation, RekordboxLayout? Layout, double Scale, ScreenPoint Offset, int? ProcessId,
    IntPtr Window, string? Version, NativeMethods.WindowRect ReadRect = default)
{
    public static RekordboxState Initial { get; } = new(OverlayActivation.RekordboxNotRunning, null, 1, default, null, IntPtr.Zero, null);
}

/// Watches rekordbox (RekordboxMonitor.swift): ten times a second, on its own thread, it finds rekordbox's process and
/// main window, reads the window through UI Automation, locates the decks and evaluates the activation, and publishes
/// the state on the dispatcher only when it changed. The whole tree is read once a second (less often when a read is
/// slow: LiveLayout.FullReadInterval), and at once when the window is resized or one of its popups closes; in between
/// only the header BPMs and the times are read again, and a moved window only moves the offset. WinEvent hooks (another app in front, rekordbox minimized, moved or shown) poll at once, as the macOS
/// workspace and AX notifications do. It only reads rekordbox.
internal sealed class RekordboxMonitor : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan StaleReadInterval = TimeSpan.FromMilliseconds(250);
    private static readonly bool IsTracing = Environment.GetEnvironmentVariable("EARMATCH_TRACE") == "1";
    private static readonly TimeSpan ProcessLookupInterval = TimeSpan.FromSeconds(1);

    private readonly Dispatcher _dispatcher;
    private readonly HookThread _hooks;
    private readonly Thread _thread;
    private readonly AutoResetEvent _wake = new(false);
    private readonly UiaWindowReader _reader = new();
    private readonly StallBackoff _backoff = new(TimeSpan.FromSeconds(2));
    private readonly NativeMethods.WinEventProc _onEvent;
    private readonly List<IntPtr> _systemHooks = [];
    private readonly List<IntPtr> _processHooks = [];
    private volatile bool _isStopping;

    // The monitor thread's state.
    private RekordboxProcess? _process;
    private DateTime _lastProcessLookup = DateTime.MinValue;
    private IntPtr _window;
    private LiveLayout? _live;
    private NativeMethods.WindowRect _readRect;
    private DateTime _lastFullRead;
    private TimeSpan _fullReadInterval = LiveLayout.FullReadInterval(TimeSpan.Zero);
    private volatile bool _isFullReadRequested;
    private RekordboxState _published = RekordboxState.Initial;
    private int _hookedProcess;

    // Read by the WinEvent callback on the hook thread.
    private nint _watchedWindow;

    public RekordboxMonitor(Dispatcher dispatcher, HookThread hooks)
    {
        _dispatcher = dispatcher;
        _hooks = hooks;
        _onEvent = OnWinEvent;
        _thread = new Thread(Run) { IsBackground = true, Name = "Earmatch rekordbox monitor" };
        _thread.SetApartmentState(ApartmentState.MTA);
    }

    /// The last published state, on the dispatcher.
    public RekordboxState State { get; private set; } = RekordboxState.Initial;

    /// Raised on the dispatcher when the state changed.
    public event Action<RekordboxState>? Changed;

    public void Start()
    {
        _hooks.Post(() =>
        {
            // Earmatch's own window coming to the front counts too: it pauses the overlays at once.
            const uint flags = NativeMethods.WinEventOutOfContext;
            _systemHooks.Add(NativeMethods.SetWinEventHook(NativeMethods.EventSystemForeground, NativeMethods.EventSystemForeground, IntPtr.Zero,
                _onEvent, 0, 0, flags));
            _systemHooks.Add(NativeMethods.SetWinEventHook(NativeMethods.EventSystemMinimizeStart, NativeMethods.EventSystemMinimizeEnd, IntPtr.Zero,
                _onEvent, 0, 0, flags));
        });
        _thread.Start();
    }

    /// Polls at once instead of at the next tick.
    public void RequestPoll() => _wake.Set();

    private void Run()
    {
        while (!_isStopping)
        {
            var state = Poll();
            if (state != _published)
            {
                if (IsTracing) Console.Error.WriteLine($"earmatch: {L10n.English(() => state.Activation.Message)} offset {state.Offset}");
                _published = state;
                _dispatcher.BeginInvoke(() =>
                {
                    State = state;
                    Changed?.Invoke(state);
                });
            }
            _wake.WaitOne(TickInterval);
        }
        _process?.Dispose();
    }

    private RekordboxState Poll()
    {
        var now = DateTime.UtcNow;
        if (!TrackProcess(now) || _process is not { } process) return RekordboxState.Initial;
        if (_window == IntPtr.Zero || !NativeMethods.IsWindow(_window))
        {
            ForgetWindow();
            _window = process.MainWindow();
            _watchedWindow = _window;
        }
        var waiting = new RekordboxState(OverlayActivation.MainWindowNotFound, null, 1, default, process.Id, _window, process.Version);
        if (_window == IntPtr.Zero || !NativeMethods.GetWindowRect(_window, out var rect)) return waiting;
        if (!_backoff.AllowsPoll(now)) return waiting;
        try
        {
            var needsFullRead = _live is null || _isFullReadRequested || now - _lastFullRead >= (_reader.IsStale ? StaleReadInterval : _fullReadInterval)
                || rect.Width != _readRect.Width || rect.Height != _readRect.Height;
            // A minimized window keeps the layout it had: its elements are off screen until it comes back.
            if (needsFullRead && !(NativeMethods.IsIconic(_window) && _live is not null)) FullRead(now, rect);
            if (_live is not { } live) return waiting;
            var layout = live.Refresh(_reader.ReadWatched());
            // The reads above can block for a while (rekordbox answers UI Automation on its own window thread): the window's
            // place, state and the app in front are sampled after them, so a switch to another app during a read is not lost.
            if (!NativeMethods.GetWindowRect(_window, out rect)) return waiting;
            var isFrontmost = NativeMethods.GetForegroundWindow() == _window && !NativeMethods.IsIconic(_window);
            var offset = new ScreenPoint(rect.Left - _readRect.Left, rect.Top - _readRect.Top);
            return new RekordboxState(OverlayActivation.Evaluate(isRunning: true, layout, isFrontmost), layout, live.Snapshot.Scale, offset,
                process.Id, _window, process.Version, _readRect);
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            // rekordbox did not answer, or its window went away while it was read: wait a moment before reading again.
            ForgetWindow();
            _backoff.RecordStall(now);
            return waiting;
        }
    }

    /// Reads the whole window and locates the decks. A read taken while a popup menu of rekordbox is open keeps the
    /// layout read before it, when the window kept its size.
    private void FullRead(DateTime now, NativeMethods.WindowRect rect)
    {
        _lastFullRead = now;
        _isFullReadRequested = false;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var read = _reader.ReadWindow(_window);
        _fullReadInterval = LiveLayout.FullReadInterval(watch.Elapsed);
        var live = new LiveLayout(read.Snapshot);
        var keeps = _live is not null && LiveLayout.IsTransientRead(live.Layout) && rect.Width == _readRect.Width && rect.Height == _readRect.Height;
        if (IsTracing)
        {
            Console.Error.WriteLine($"earmatch: full read of {read.Snapshot.Elements.Count} elements in {watch.Elapsed.TotalMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} ms"
                + (keeps ? ", transient: kept the layout" : ""));
        }
        if (keeps) return;
        _live = live;
        _readRect = rect;
        _reader.Adopt(read, live.WatchedElements);
    }

    /// Whether rekordbox runs; it is looked up at most once a second while it does not.
    private bool TrackProcess(DateTime now)
    {
        if (_process is { IsRunning: true }) return true;
        if (_process is not null)
        {
            _process.Dispose();
            _process = null;
            ForgetWindow();
            HookProcess(0);
        }
        if (now - _lastProcessLookup < ProcessLookupInterval) return false;
        _lastProcessLookup = now;
        _process = RekordboxProcess.Find();
        if (_process is null) return false;
        HookProcess(_process.Id);
        return true;
    }

    private void ForgetWindow()
    {
        _window = IntPtr.Zero;
        _watchedWindow = IntPtr.Zero;
        _live = null;
        _reader.Reset();
    }

    /// Hooks rekordbox's own window events (shown, hidden, moved or resized), which come from its process only.
    private void HookProcess(int processId)
    {
        if (processId == _hookedProcess) return;
        _hookedProcess = processId;
        _hooks.Post(() =>
        {
            foreach (var hook in _processHooks) NativeMethods.UnhookWinEvent(hook);
            _processHooks.Clear();
            if (processId == 0) return;
            const uint flags = NativeMethods.WinEventOutOfContext;
            _processHooks.Add(NativeMethods.SetWinEventHook(NativeMethods.EventObjectShow, NativeMethods.EventObjectHide, IntPtr.Zero, _onEvent,
                (uint)processId, 0, flags));
            _processHooks.Add(NativeMethods.SetWinEventHook(NativeMethods.EventObjectLocationChange, NativeMethods.EventObjectLocationChange,
                IntPtr.Zero, _onEvent, (uint)processId, 0, flags));
        });
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr window, int idObject, int idChild, uint thread, uint time)
    {
        switch (eventType)
        {
            case NativeMethods.EventSystemForeground or NativeMethods.EventSystemMinimizeStart or NativeMethods.EventSystemMinimizeEnd:
                _wake.Set();
                break;
            case NativeMethods.EventObjectLocationChange when window == _watchedWindow && idObject == NativeMethods.ObjIdWindow:
                _wake.Set();
                break;
            case NativeMethods.EventObjectShow or NativeMethods.EventObjectHide when idObject == NativeMethods.ObjIdWindow:
                // A popup of rekordbox opened or closed (the layout or mode menu): its window may have a new layout.
                if (window != _watchedWindow && NativeMethods.IsTopLevel(window)) _isFullReadRequested = true;
                _wake.Set();
                break;
        }
    }

    private static bool IsReadFailure(Exception exception) =>
        exception is ElementNotAvailableException or TimeoutException or COMException or InvalidOperationException or ArgumentException
            or Win32Exception or UnauthorizedAccessException or FormatException;

    public void Dispose()
    {
        _isStopping = true;
        _wake.Set();
        if (_thread.IsAlive) _thread.Join(TimeSpan.FromSeconds(2));
        _hooks.Post(() =>
        {
            foreach (var hook in _systemHooks.Concat(_processHooks)) NativeMethods.UnhookWinEvent(hook);
            _systemHooks.Clear();
            _processHooks.Clear();
        });
    }
}
