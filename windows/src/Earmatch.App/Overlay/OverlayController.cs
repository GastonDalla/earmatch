using System.Windows.Threading;
using Earmatch.App.Capture;
using Earmatch.App.Rekordbox;
using Earmatch.Core;

namespace Earmatch.App.Overlay;

internal sealed class OverlayController : IDisposable
{
    private const double StaleFrameAge = 0.25;
    private const double LiveUpdateInterval = 1.0 / 30;

    private readonly Dispatcher _dispatcher;
    private readonly WindowCapture _capture;
    private readonly FrameReader _reader = new();
    private readonly FrameHandoff _handoff = new();
    private readonly PhaseMeterBoard _board;
    private readonly RefreshDriver _refresh = new();
    private RekordboxState _state = RekordboxState.Initial;
    private OverlaySettings _settings = new();
    private bool _isPeeking;
    private CapturedFrame? _latest;
    private bool _isVisible;
    private bool _wasActive;
    private double? _committedFrameTime;
    private double _lastLiveUpdate;
    private bool _isLiveFeedSending;
    private bool _isDisposed;

    private OverlayWindow? _panelWindow;
    private readonly WaveformPanelView _panel = new();
    private readonly Dictionary<int, (OverlayWindow Window, MeterView View)> _bands = [];
    private readonly Dictionary<int, (OverlayWindow Window, MeterView View)> _sideMeters = [];
    private readonly Dictionary<int, (OverlayWindow Window, BPMMaskView View)> _masks = [];
    private readonly Dictionary<(OverlayCoverKind Kind, int Deck), OverlayWindow> _covers = [];

    public OverlayController(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _board = new PhaseMeterBoard(_reader.Stats);
        _capture = new WindowCapture(dispatcher);
        _capture.FrameArrived = raw =>
        {
            if (_reader.Read(raw) is not { } frame) return;
            if (_handoff.Put(frame)) _dispatcher.BeginInvoke(FrameArrived, DispatcherPriority.Render);
        };
        _capture.StatusChanged += status =>
        {
            if (_isDisposed) return;
            if (!status.IsRunning) DropFrames();
            Render();
            CaptureStatusChanged?.Invoke();
        };
        _refresh.Refresh = Refresh;
    }

    public event Action? CaptureStatusChanged;

    public event Action<LiveDecksSnapshot>? LiveUpdate;

    public string? CaptureMessage
    {
        get
        {
            if (_board.Latest?.MappingProblem is { } problem) return L10n.Format("Capture does not match the rekordbox window: %@", problem.Message);
            return _capture.Status.Kind == CaptureStatusKind.Failed ? _capture.Status.Message : null;
        }
    }

    public void Update(RekordboxState state, OverlaySettings settings, bool isPeeking)
    {
        _state = state;
        _settings = settings;
        _isPeeking = isPeeking;
        Render();
    }

    public CaptureDiagnostics Diagnostics(string earmatchVersion, string windowsVersion) => L10n.English(() =>
    {
        var stats = _reader.Stats.Current;
        var decks = (_state.Layout?.Decks ?? []).Select(deck =>
        {
            var reading = _board.Latest?.Decks.GetValueOrDefault(deck.Number);
            return new CaptureDiagnostics.Deck(deck.Number, PhaseMeterBoard.IsLoaded(deck),
                reading is null ? "-" : reading.Phase.Status.ToString().ToLowerInvariant(), reading?.Phase.BeatSpacing, reading?.Phase.PhaseInBar,
                reading?.BeatsPerSecond, reading?.IsMaster ?? false, deck.BPM, deck.ElapsedSeconds);
        }).ToList();
        return new CaptureDiagnostics(earmatchVersion, windowsVersion, _state.Version, _state.Activation.Message, _state.Layout?.LayoutName,
            _capture.Status.Message, _board.Latest?.MappingProblem?.Message, _state.Layout?.StackedWaveform is not null, stats.FramesPerSecond,
            stats.CaptureJitterMilliseconds, stats.AverageProcessingMilliseconds, stats.MaxProcessingMilliseconds,
            stats.AverageEarliestLatencyMilliseconds, stats.AverageLatencyMilliseconds, stats.MaxLatencyMilliseconds, stats.RefreshesPerSecond,
            stats.PredictionErrorMilliseconds, decks);
    });

    private bool NeedsPixels =>
        (_settings.IsBPMLimitEnabled && _settings.BPMLimit != BPMLimit.CoverFully)
        || _settings.WaveformMode == WaveformMode.Split
        || _settings.IsPhaseMeterEnabled
        || _settings.IsMixTrainerEnabled;

    private double Scale => _state.Scale > 0 ? _state.Scale : 1;

    private void Render()
    {
        if (_isDisposed) return;
        SyncCapture();
        var layout = _state.Layout;
        var analyses = _settings.IsEnabled && _state.Activation.IsActive && layout is not null && _state.Window != IntPtr.Zero;
        var visible = analyses && !_isPeeking;
        if (visible && !_isVisible)
        {
            var cutoff = CaptureClock.Now - StaleFrameAge;
            _handoff.Discard(cutoff);
            if (_latest is { } latest && latest.Time < cutoff) DropFrames(keepingPending: true);
        }
        _isVisible = visible;
        var options = new FrameAnalysisOptions(_settings.IsBPMLimitEnabled, _settings.IsPhaseMeterEnabled && _settings.CounterStyle == CounterStyle.CueCountdown);
        _reader.Configure(analyses ? new FrameReaderInput(layout!.Decks, _state.ReadRect, Scale, options) : null);
        if (!visible || layout is null)
        {
            _refresh.Stop();
            HideAll();
            StopLiveFeed();
            return;
        }
        _refresh.Start();
        PlaceWaveform(layout);
        PlaceMasks(layout);
        PlaceBlindCovers(layout);
        _board.Invalidate();
        DrawFrame(layout);
    }

    private void SyncCapture()
    {
        var isActive = _state.Activation.IsActive;
        if (_settings.IsEnabled && NeedsPixels && _state.ProcessId is not null && _state.Window != IntPtr.Zero)
        {
            _capture.Follow(_state.Window);
            if (isActive && !_wasActive) _capture.WindowMayBeBack();
        }
        else
        {
            _capture.Follow(IntPtr.Zero);
        }
        _wasActive = isActive;
    }

    private void PlaceWaveform(RekordboxLayout layout)
    {
        var bandSize = new ScreenSize(MeterView.CompactWidth * Scale, MeterView.CompactHeight * Scale);
        var plan = WaveformOverlayPlan.Create(layout, _settings.WaveformMode, _settings.IsPhaseMeterEnabled, bandSize);
        PlaceMeters(_sideMeters, plan?.SideMeters ?? new Dictionary<int, ScreenRect>());
        var bands = plan is not null && layout.StackedWaveform is { } stacked
            ? plan.Bands.ToDictionary(entry => entry.Key, entry => entry.Value.Offset(stacked.MinX, stacked.MinY))
            : [];
        PlaceMeters(_bands, bands);
        if (plan?.Panel is not { } panel || plan.Panes.Count == 0)
        {
            _panelWindow?.Hide();
            return;
        }
        var strips = layout.Decks.Where(deck => deck.WaveformStrip is not null).ToDictionary(deck => deck.Number, deck => deck.WaveformStrip!.Value);
        _panel.Configure(plan, strips, Scale);
        _panelWindow ??= new OverlayWindow(_panel, isOpaque: true);
        _panelWindow.Place(panel, _state.Offset);
    }

    private void PlaceMeters(Dictionary<int, (OverlayWindow Window, MeterView View)> meters, IReadOnlyDictionary<int, ScreenRect> frames)
    {
        foreach (var (number, frame) in frames)
        {
            if (!meters.TryGetValue(number, out var meter))
            {
                var view = new MeterView { RowsCenter = 0.5, Plate = OverlayColors.Band };
                meter = (new OverlayWindow(view), view);
                meters[number] = meter;
            }
            meter.Window.Place(frame, _state.Offset);
        }
        foreach (var (number, meter) in meters)
        {
            if (!frames.ContainsKey(number)) meter.Window.Hide();
        }
    }

    private bool ShowsMeters => _settings.IsPhaseMeterEnabled
        && (_panelWindow?.IsShown == true || _bands.Values.Any(meter => meter.Window.IsShown) || _sideMeters.Values.Any(meter => meter.Window.IsShown));

    private void PlaceMasks(RekordboxLayout layout)
    {
        var shown = new HashSet<int>();
        if (_settings.IsBPMLimitEnabled)
        {
            foreach (var deck in layout.Decks)
            {
                if (deck.JogBPMFrame is not { } jog) continue;
                if (!_masks.TryGetValue(deck.Number, out var mask))
                {
                    var view = new BPMMaskView();
                    mask = (new OverlayWindow(view), view);
                    _masks[deck.Number] = mask;
                }
                mask.Window.Place(Outset(jog, BPMMaskView.CoverMargin * Scale), _state.Offset);
                shown.Add(deck.Number);
            }
        }
        foreach (var (number, mask) in _masks)
        {
            if (!shown.Contains(number)) mask.Window.Hide();
        }
    }

    private void PlaceBlindCovers(RekordboxLayout layout)
    {
        var shown = new HashSet<(OverlayCoverKind, int)>();
        foreach (var cover in OverlayCovers.Plan(layout, _settings, Scale))
        {
            if (cover.Kind is not (OverlayCoverKind.Pitch or OverlayCoverKind.HeaderBPM)) continue;
            var key = (cover.Kind, cover.Deck);
            if (!_covers.TryGetValue(key, out var window))
            {
                window = new OverlayWindow(cover.Kind == OverlayCoverKind.HeaderBPM ? OverlayColors.HeaderGrey : System.Windows.Media.Colors.Black);
                _covers[key] = window;
            }
            window.Place(cover.Frame, _state.Offset);
            shown.Add(key);
        }
        foreach (var (key, window) in _covers)
        {
            if (!shown.Contains(key)) window.Hide();
        }
    }

    public void HideAll()
    {
        _panelWindow?.Hide();
        foreach (var meter in _bands.Values.Concat(_sideMeters.Values)) meter.Window.Hide();
        foreach (var mask in _masks.Values) mask.Window.Hide();
        foreach (var window in _covers.Values) window.Hide();
    }

    private void FrameArrived()
    {
        if (!_isVisible || _state.Layout is not { } layout || !TakeNewestFrame(layout)) return;
        DrawFrame(layout);
        if (_latest is { } latest)
        {
            _reader.Stats.RecordCommit(CaptureClock.Now - latest.Time);
            _committedFrameTime = latest.Time;
        }
    }

    private void Refresh(double timestamp, double targetTime)
    {
        if (!_isVisible || _state.Layout is not { } layout) return;
        var started = CaptureClock.Now;
        var newFrame = TakeNewestFrame(layout);
        var liveDue = LiveUpdate is not null && targetTime - _lastLiveUpdate >= LiveUpdateInterval - 0.002;
        var drawsMeters = ShowsMeters;
        if (newFrame) DrawFrame(layout);
        if (drawsMeters || liveDue) _board.Compute(layout, _settings, targetTime);
        if (_panelWindow?.IsShown == true)
        {
            foreach (var deck in layout.Decks) _panel.ShowMeter(drawsMeters ? _board.Display(deck.Number) : null, deck.Number);
        }
        foreach (var (number, meter) in _bands.Concat(_sideMeters))
        {
            if (meter.Window.IsShown) meter.View.Display = drawsMeters ? _board.Display(number) : null;
        }
        var stats = _reader.Stats;
        if (newFrame && _latest is { } latest)
        {
            stats.RecordCommit(CaptureClock.Now - latest.Time);
            stats.RecordScreen(targetTime - latest.Time, targetTime - latest.Time);
        }
        else if (_committedFrameTime is { } committed)
        {
            stats.RecordScreen(timestamp - committed, targetTime - committed);
        }
        else
        {
            stats.RecordRepeatedFrame();
        }
        _committedFrameTime = null;
        if (liveDue)
        {
            _lastLiveUpdate = targetTime;
            SendLive(layout);
        }
        stats.RecordRefresh(CaptureClock.Now - started);
    }

    private bool TakeNewestFrame(RekordboxLayout layout)
    {
        if (_handoff.Take() is not { } frame) return false;
        if (!_capture.Status.IsRunning || frame.Generation != _capture.Generation || !_board.Accept(frame.Analysis, layout, out var problemChanged))
        {
            frame.Pixels.Release();
            return false;
        }
        _latest?.Pixels.Release();
        _latest = frame;
        if (problemChanged) CaptureStatusChanged?.Invoke();
        return true;
    }

    private void DropFrames(bool keepingPending = false)
    {
        if (!keepingPending) _handoff.Take()?.Pixels.Release();
        _latest?.Pixels.Release();
        _latest = null;
        _committedFrameTime = null;
        _panel.ShowFrame(null, null);
        if (_board.Reset()) CaptureStatusChanged?.Invoke();
    }

    private void DrawFrame(RekordboxLayout layout)
    {
        var frame = _board.HasFrame ? _latest : null;
        if (_panelWindow?.IsShown == true) _panel.ShowFrame(frame, frame is null ? null : _board.Master);
        foreach (var deck in layout.Decks)
        {
            if (!_masks.TryGetValue(deck.Number, out var mask) || !mask.Window.IsShown || deck.JogBPMFrame is not { } jog) continue;
            var measured = frame?.Analysis.Decks.GetValueOrDefault(deck.Number)?.BPMCovers?.Cover(_settings.BPMLimit);
            var cover = measured ?? Outset(jog, Scale);
            mask.View.Show(cover.Offset(_state.Offset.X, _state.Offset.Y), mask.Window.PlacedOrigin, Scale);
        }
    }

    private void SendLive(RekordboxLayout layout)
    {
        if (LiveUpdate is not { } send) return;
        var stats = _reader.Stats.Current;
        _isLiveFeedSending = true;
        send(_board.LiveSnapshot(layout, stats.FramesPerSecond, stats.AverageLatencyMilliseconds, CaptureClock.Now));
    }

    private void StopLiveFeed()
    {
        if (!_isLiveFeedSending) return;
        _isLiveFeedSending = false;
        _lastLiveUpdate = 0;
        LiveUpdate?.Invoke(LiveDecksSnapshot.Empty);
    }

    private static ScreenRect Outset(ScreenRect rect, double by) => new(rect.X - by, rect.Y - by, rect.Width + 2 * by, rect.Height + 2 * by);

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _refresh.Stop();
        _capture.Dispose();
        DropFrames();
        _panelWindow?.Dispose();
        foreach (var meter in _bands.Values.Concat(_sideMeters.Values)) meter.Window.Dispose();
        foreach (var mask in _masks.Values) mask.Window.Dispose();
        foreach (var window in _covers.Values) window.Dispose();
    }
}
