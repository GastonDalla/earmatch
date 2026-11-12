using System.Diagnostics;
using System.Windows.Threading;
using Earmatch.Core;

namespace Earmatch.App.Practice;

internal sealed class PracticeController
{
    private readonly PracticeStore _store;
    private readonly Dispatcher _dispatcher;
    private readonly PracticeTrainer _trainer = new(DateTimeOffset.Now);
    private readonly SerialWorkQueue _queue = new(error => Trace.WriteLine($"earmatch: practice history: {error.Message}"));
    private DispatcherTimer? _gapCheck;

    public PracticeController(PracticeStore store, Dispatcher dispatcher)
    {
        _store = store;
        _dispatcher = dispatcher;
    }

    public event Action? Changed;

    public PracticeOverview Overview { get; private set; } = PracticeOverview.Empty;

    public PracticeLiveStatus? Live => _trainer.Live;

    public void LoadHistory() => Enqueue(_store.Load);

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

    public bool Flush(TimeSpan timeout) => _queue.Flush(timeout);

    private static DateTimeOffset DateAt(double time) => DateTimeOffset.Now.AddSeconds(-time);

    private void Enqueue(Func<PracticeOverview> work) => _queue.Enqueue(() =>
    {
        var overview = work();
        _dispatcher.BeginInvoke(() =>
        {
            if (overview == Overview) return;
            Overview = overview;
            Changed?.Invoke();
        });
    });
}
