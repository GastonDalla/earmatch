using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Threading;
using Earmatch.Core;

namespace Earmatch.App.Rekordbox;

internal sealed record RekordboxState(OverlayActivation Activation, RekordboxLayout? Layout, double Scale, ScreenPoint Offset, int? ProcessId,
    IntPtr Window, string? Version, NativeMethods.WindowRect ReadRect = default)
{
    public static RekordboxState Initial { get; } = new(OverlayActivation.RekordboxNotRunning, null, 1, default, null, IntPtr.Zero, null);
}

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

    private nint _watchedWindow;

    public RekordboxMonitor(Dispatcher dispatcher, HookThread hooks)
    {
        _dispatcher = dispatcher;
        _hooks = hooks;
        _onEvent = OnWinEvent;
        _thread = new Thread(Run) { IsBackground = true, Name = "Earmatch rekordbox monitor" };
        _thread.SetApartmentState(ApartmentState.MTA);
    }

    public RekordboxState State { get; private set; } = RekordboxState.Initial;

    public event Action<RekordboxState>? Changed;

    public void Start()
    {
        _hooks.Post(() =>
        {
            const uint flags = NativeMethods.WinEventOutOfContext;
            _systemHooks.Add(NativeMethods.SetWinEventHook(NativeMethods.EventSystemForeground, NativeMethods.EventSystemForeground, IntPtr.Zero,
                _onEvent, 0, 0, flags));
            _systemHooks.Add(NativeMethods.SetWinEventHook(NativeMethods.EventSystemMinimizeStart, NativeMethods.EventSystemMinimizeEnd, IntPtr.Zero,
                _onEvent, 0, 0, flags));
        });
        _thread.Start();
    }

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
            if (needsFullRead && !(NativeMethods.IsIconic(_window) && _live is not null)) FullRead(now, rect);
            if (_live is not { } live) return waiting;
            var layout = live.Refresh(_reader.ReadWatched());
            if (!NativeMethods.GetWindowRect(_window, out rect)) return waiting;
            var isFrontmost = NativeMethods.GetForegroundWindow() == _window && !NativeMethods.IsIconic(_window);
            var offset = new ScreenPoint(rect.Left - _readRect.Left, rect.Top - _readRect.Top);
            return new RekordboxState(OverlayActivation.Evaluate(isRunning: true, layout, isFrontmost), layout, live.Snapshot.Scale, offset,
                process.Id, _window, process.Version, _readRect);
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            ForgetWindow();
            _backoff.RecordStall(now);
            return waiting;
        }
    }

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
