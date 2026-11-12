using System.Diagnostics;
using System.Windows.Threading;
using Earmatch.Core;

namespace Earmatch.App.Practice;

/// The mix trainer behind the Practice pane (PracticeController.swift): every live snapshot of the decks goes to the
/// PracticeTrainer; a finished mix is recorded and saved off the window's thread, in order, and the history the pane
/// shows follows. While a mix is open and nothing is read, a timer closes it once the gap is long enough. Used on the
/// window's thread.
internal sealed class PracticeController
{
    private readonly PracticeStore _store;
    private readonly Dispatcher _dispatcher;
    private readonly PracticeTrainer _trainer = new(DateTimeOffset.Now);
    private Task _queue = Task.CompletedTask;
    private DispatcherTimer? _gapCheck;

    public PracticeController(PracticeStore store, Dispatcher dispatcher)
    {
        _store = store;
        _dispatcher = dispatcher;
    }

    /// Raised on the window's thread when the history or the transition being timed changed.
    public event Action? Changed;

    public PracticeOverview Overview { get; private set; } = PracticeOverview.Empty;

    public PracticeLiveStatus? Live => _trainer.Live;

    public void LoadHistory() => Enqueue(_store.Load);

    /// Feeds one live snapshot taken at `time` (seconds on the capture clock).
    public void Ingest(LiveDecksSnapshot snapshot, double time)
    {
        var step = _trainer.Ingest(snapshot, time, DateAt(time));
        if (step.Result is { } result) Enqueue(() => _store.Record(result));
        if (step.LiveChanged) Changed?.Invoke();
        if (snapshot.Decks.Count > 0)
        {
            _gapCheck?.Stop();
            _gapCheck = null;
        }
        else if (step.NeedsGapCheck && _gapCheck is null)
        {
            var timer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
            {
                Interval = TimeSpan.FromSeconds(PracticeSession.GapTolerance + 0.25),
            };
            timer.Tick += (_, _) => CloseIdleMix(timer);
            _gapCheck = timer;
            timer.Start();
        }
    }

    private void CloseIdleMix(DispatcherTimer timer)
    {
        timer.Stop();
        if (!ReferenceEquals(timer, _gapCheck)) return;
        _gapCheck = null;
        var now = Capture.CaptureClock.Now;
        var (result, changed) = _trainer.Advance(now, DateAt(now));
        if (result is not null) Enqueue(() => _store.Record(result));
        if (changed) Changed?.Invoke();
    }

    /// The date of time zero on the capture clock, so the trainer can date `time`.
    private static DateTimeOffset DateAt(double time) => DateTimeOffset.Now.AddSeconds(-time);

    /// Runs a store call after the earlier ones, off the window's thread, and shows its history.
    private void Enqueue(Func<PracticeOverview> work)
    {
        _queue = _queue.ContinueWith(_ =>
        {
            try
            {
                var overview = work();
                _dispatcher.BeginInvoke(() =>
                {
                    if (overview == Overview) return;
                    Overview = overview;
                    Changed?.Invoke();
                });
            }
            catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Trace.WriteLine($"earmatch: practice history: {error.Message}");
            }
        }, TaskScheduler.Default);
    }
}
