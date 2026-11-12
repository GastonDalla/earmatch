using System.Globalization;

namespace Earmatch.Core;

/// How the capture keeps up (FrameStats in FrameAnalyzer.swift): frames per second and their jitter, analysis time,
/// how long a frame takes from capture to the screen, and how often the meters are drawn. The counters close a window
/// once a second of frames has gone by; Current is the last closed window. Safe to call from the capture thread and the
/// window's thread at once.
public sealed class FrameStats
{
    public sealed record Snapshot
    {
        public double FramesPerSecond { get; init; }
        public double AverageProcessingMilliseconds { get; init; }
        public double MaxProcessingMilliseconds { get; init; }
        public double AverageLatencyMilliseconds { get; init; }
        public double MaxLatencyMilliseconds { get; init; }
        public double AverageEarliestLatencyMilliseconds { get; init; }
        public double AverageCommitMilliseconds { get; init; }
        public double CaptureJitterMilliseconds { get; init; }
        public double RefreshesPerSecond { get; init; }
        public double RepeatedFramesPerSecond { get; init; }
        public double AverageRefreshMicroseconds { get; init; }
        public double MaxRefreshMicroseconds { get; init; }
        public double? PredictionErrorMilliseconds { get; init; }
    }

    private readonly object _gate = new();
    private readonly bool _isTracing;
    private double? _windowStart;
    private int _frames;
    private double _totalProcessing;
    private double _maxProcessing;
    private double? _lastFrameTime;
    private int _intervals;
    private double _intervalSum;
    private double _intervalSquares;
    private double _totalLatency;
    private double _totalEarliestLatency;
    private double _maxLatency;
    private int _latencySamples;
    private double _totalCommit;
    private int _commitSamples;
    private int _repeatedFrames;
    private int _refreshes;
    private double _totalRefreshWork;
    private double _maxRefreshWork;
    private readonly Dictionary<int, int> _heldFrames = [];
    private readonly Dictionary<int, int> _lostFrames = [];
    private Snapshot _last = new();

    /// With tracing on (EARMATCH_TRACE=1 in the app), Record returns a line of figures each time a window closes.
    public FrameStats(bool isTracing = false)
    {
        _isTracing = isTracing;
    }

    public Snapshot Current
    {
        get
        {
            lock (_gate) return _last;
        }
    }

    /// A frame was analysed in `processingSeconds`; `frameTime` is when it was captured. Returns the trace line of a
    /// window it closed, while tracing.
    public string? Record(double processingSeconds, double frameTime, IReadOnlyDictionary<int, DeckFrameReading> decks)
    {
        lock (_gate)
        {
            _frames++;
            _totalProcessing += processingSeconds;
            _maxProcessing = Math.Max(_maxProcessing, processingSeconds);
            if (_lastFrameTime is { } previous && frameTime > previous)
            {
                var interval = frameTime - previous;
                _intervals++;
                _intervalSum += interval;
                _intervalSquares += interval * interval;
            }
            _lastFrameTime = frameTime;
            foreach (var (number, deck) in decks)
            {
                if (deck.Phase.Status == PhaseTrackerStatus.Held) _heldFrames[number] = _heldFrames.GetValueOrDefault(number) + 1;
                if (deck.Phase.Status == PhaseTrackerStatus.Lost) _lostFrames[number] = _lostFrames.GetValueOrDefault(number) + 1;
            }
            if (_windowStart is not { } start)
            {
                _windowStart = frameTime;
                return null;
            }
            var elapsed = frameTime - start;
            if (elapsed < 1) return null;
            var jitter = _last.CaptureJitterMilliseconds;
            if (_intervals > 1)
            {
                var mean = _intervalSum / _intervals;
                jitter = Math.Sqrt(Math.Max(0, _intervalSquares / _intervals - mean * mean)) * 1000;
            }
            _last = new Snapshot
            {
                FramesPerSecond = (_frames - 1) / elapsed,
                AverageProcessingMilliseconds = _totalProcessing / _frames * 1000,
                MaxProcessingMilliseconds = _maxProcessing * 1000,
                AverageLatencyMilliseconds = _latencySamples > 0 ? _totalLatency / _latencySamples * 1000 : 0,
                AverageEarliestLatencyMilliseconds = _latencySamples > 0 ? _totalEarliestLatency / _latencySamples * 1000 : 0,
                MaxLatencyMilliseconds = _maxLatency * 1000,
                AverageCommitMilliseconds = _commitSamples > 0 ? _totalCommit / _commitSamples * 1000 : 0,
                RepeatedFramesPerSecond = _repeatedFrames / elapsed,
                CaptureJitterMilliseconds = jitter,
                RefreshesPerSecond = _refreshes / elapsed,
                AverageRefreshMicroseconds = _refreshes > 0 ? _totalRefreshWork / _refreshes * 1_000_000 : 0,
                MaxRefreshMicroseconds = _maxRefreshWork * 1_000_000,
            };
            var trace = _isTracing ? TraceLine(decks) : null;
            _windowStart = frameTime;
            _frames = 1;
            _totalProcessing = _maxProcessing = 0;
            _intervals = 0;
            _intervalSum = _intervalSquares = 0;
            _totalLatency = _totalEarliestLatency = _maxLatency = 0;
            _latencySamples = 0;
            _totalCommit = 0;
            _commitSamples = 0;
            _repeatedFrames = 0;
            _refreshes = 0;
            _totalRefreshWork = _maxRefreshWork = 0;
            _heldFrames.Clear();
            _lostFrames.Clear();
            return trace;
        }
    }

    /// A frame reached the overlays `seconds` after it was captured.
    public void RecordCommit(double seconds)
    {
        lock (_gate)
        {
            _totalCommit += seconds;
            _commitSamples++;
        }
    }

    /// A refresh put a frame on screen: between `earliest` and `latest` seconds after it was captured.
    public void RecordScreen(double earliest, double latest)
    {
        lock (_gate)
        {
            _totalEarliestLatency += earliest;
            _totalLatency += latest;
            _maxLatency = Math.Max(_maxLatency, latest);
            _latencySamples++;
        }
    }

    /// A refresh came with no frame drawn yet.
    public void RecordRepeatedFrame()
    {
        lock (_gate) _repeatedFrames++;
    }

    /// A refresh of the overlays took `workSeconds`.
    public void RecordRefresh(double workSeconds)
    {
        lock (_gate)
        {
            _refreshes++;
            _totalRefreshWork += workSeconds;
            _maxRefreshWork = Math.Max(_maxRefreshWork, workSeconds);
        }
    }

    private string TraceLine(IReadOnlyDictionary<int, DeckFrameReading> decks)
    {
        static string F(string format, double value) => value.ToString(format, CultureInfo.InvariantCulture);
        var decksText = string.Join(" ", decks.Keys.Order().Select(number =>
        {
            var deck = decks[number];
            var phase = deck.Phase.PhaseInBar is { } p ? F("F2", p) : "-";
            var rate = deck.BeatsPerSecond is { } r ? F("F3", r) : "-";
            return $"deck{number}={deck.Phase.Status} phase={phase} bps={rate} master={deck.IsMaster} held={_heldFrames.GetValueOrDefault(number)} "
                + $"lost={_lostFrames.GetValueOrDefault(number)}";
        }));
        var last = _last;
        return $"earmatch: {F("F1", last.FramesPerSecond)} fps (interval sd {F("F2", last.CaptureJitterMilliseconds)} ms), analysis avg "
            + $"{F("F2", last.AverageProcessingMilliseconds)} ms max {F("F2", last.MaxProcessingMilliseconds)} ms, capture-to-commit "
            + $"{F("F1", last.AverageCommitMilliseconds)} ms, capture-to-screen {F("F1", last.AverageEarliestLatencyMilliseconds)}.."
            + $"{F("F1", last.AverageLatencyMilliseconds)} ms (max {F("F1", last.MaxLatencyMilliseconds)}), refreshes {F("F0", last.RefreshesPerSecond)}/s "
            + $"(no new frame {F("F0", last.RepeatedFramesPerSecond)}/s) work avg {F("F0", last.AverageRefreshMicroseconds)} us max "
            + $"{F("F0", last.MaxRefreshMicroseconds)} us; {decksText}";
    }
}
