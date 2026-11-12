namespace Earmatch.Core;

public sealed class PracticeSession
{
    public const double PauseTolerance = 1;
    public const double GapTolerance = 10;
    public const double MinimumMixSeconds = 8;
    public const double MatchToleranceBPM = 0.1;
    public const double MatchHoldSeconds = 2;
    public const double TraceInterval = 0.1;
    internal const double MaximumSampleInterval = 0.25;
    internal const int TraceCapacity = 6000;

    private readonly Dictionary<int, PracticeDeckTempo> _tempos = [];
    private OpenMix? _mix;

    public PracticeSession(DateTimeOffset dateAtTimeZero)
    {
        DateAtTimeZero = dateAtTimeZero;
    }

    public DateTimeOffset DateAtTimeZero { get; set; }
    public PracticeLiveStatus? Live { get; private set; }
    public bool IsMixing => _mix is not null;

    public PracticeResult? Ingest(LiveDecksSnapshot snapshot, double time)
    {
        if (snapshot.Decks.Count == 0) return Advance(time);
        foreach (var deck in snapshot.Decks)
        {
            if (!_tempos.TryGetValue(deck.Number, out var tempo)) _tempos[deck.Number] = tempo = new PracticeDeckTempo();
            tempo.Add(deck, time);
        }
        PracticeResult? finished = null;
        if (_mix is { } open)
        {
            var outgoing = snapshot.Decks.FirstOrDefault(deck => deck.Number == open.Outgoing);
            var incoming = snapshot.Decks.FirstOrDefault(deck => deck.Number == open.Incoming);
            if (outgoing is { IsPlaying: true } && incoming is { IsPlaying: true })
            {
                open.Observe(outgoing, incoming, _tempos, time);
            }
            else if (Pair(snapshot) is not null)
            {
                finished = Close(open);
            }
            else
            {
                var isStopped = new[] { outgoing, incoming }.Any(deck => deck is { IsPlaying: false, PhaseInBar: not null });
                if (time - open.LastBothPlaying >= (isStopped ? PauseTolerance : GapTolerance)) finished = Close(open);
                else open.Interrupt();
            }
        }
        if (_mix is null && Pair(snapshot) is { } next)
        {
            var started = new OpenMix(next.Outgoing.Number, next.Incoming.Number, time);
            started.Observe(next.Outgoing, next.Incoming, _tempos, time);
            _mix = started;
        }
        Live = _mix?.Status(time);
        return finished;
    }

    public PracticeResult? Advance(double time)
    {
        if (_mix is not { } open) return null;
        if (time - open.LastBothPlaying >= GapTolerance) return Close(open);
        open.Interrupt();
        Live = open.Status(time);
        return null;
    }

    private PracticeResult? Close(OpenMix open)
    {
        _mix = null;
        Live = null;
        return open.Result(DateAtTimeZero);
    }

    private (LiveDeckSnapshot Outgoing, LiveDeckSnapshot Incoming)? Pair(LiveDecksSnapshot snapshot)
    {
        var playing = snapshot.Decks.Where(deck => deck.IsPlaying).ToList();
        if (playing.Count < 2) return null;
        double Started(LiveDeckSnapshot deck) => _tempos.TryGetValue(deck.Number, out var tempo) ? tempo.PlayingSince ?? double.PositiveInfinity : double.PositiveInfinity;
        var byStart = playing.OrderBy(Started).ThenBy(deck => deck.Number).ToList();
        var outgoing = playing.FirstOrDefault(deck => deck.IsMaster) ?? byStart[0];
        var incoming = byStart.LastOrDefault(deck => deck.Number != outgoing.Number);
        return incoming is null ? null : (outgoing, incoming);
    }

    private sealed class OpenMix
    {
        private readonly Guid _id = Guid.NewGuid();
        private readonly double _start;
        private readonly List<PracticeTraceSample> _trace = [];
        private double? _lastSample;
        private double _bothPlayingSeconds;
        private double? _candidateStart;
        private double? _lastTempoSample;
        private double? _matchedAt;
        private MatchedStats _matched = new();
        private MatchedStats _pending = new();
        private double? _tempoDifference;
        private double? _phaseError;
        private double _traceInterval = TraceInterval;

        public OpenMix(int outgoing, int incoming, double start)
        {
            Outgoing = outgoing;
            Incoming = incoming;
            _start = start;
            LastBothPlaying = start;
        }

        public int Outgoing { get; }
        public int Incoming { get; }
        public double LastBothPlaying { get; private set; }

        public void Observe(LiveDeckSnapshot deck, LiveDeckSnapshot other, IReadOnlyDictionary<int, PracticeDeckTempo> tempos, double time)
        {
            if (_lastSample is { } lastSample && time - lastSample <= MaximumSampleInterval) _bothPlayingSeconds += time - lastSample;
            _lastSample = time;
            LastBothPlaying = time;

            var outgoingTempo = tempos.TryGetValue(deck.Number, out var outgoingDeck) ? outgoingDeck.BeatsPerMinute : null;
            var incomingTempo = tempos.TryGetValue(other.Number, out var incomingDeck) ? incomingDeck.BeatsPerMinute : null;
            _tempoDifference = outgoingTempo is { } from && incomingTempo is { } to ? to - from : null;
            var slaveIsOutgoing = other.IsMaster && !deck.IsMaster;
            var slaveTempo = (slaveIsOutgoing ? outgoingTempo : incomingTempo) ?? (slaveIsOutgoing ? deck : other).BeatsPerSecond * 60;
            _phaseError = PhaseError(deck, other, slaveTempo);

            if (_matchedAt is null) UpdateMatch(time);
            if (_matchedAt is not null) _matched.Add(_phaseError, _tempoDifference);
            else if (_candidateStart is not null) _pending.Add(_phaseError, _tempoDifference);
            AppendTrace(time);
        }

        public void Interrupt()
        {
            _lastSample = null;
            _phaseError = null;
            if (_matchedAt is not null) return;
            _candidateStart = null;
            _pending = new MatchedStats();
        }

        public PracticeLiveStatus Status(double time) =>
            new(Outgoing, Incoming, time - _start, _bothPlayingSeconds, _tempoDifference, _matchedAt - _start, _phaseError, _matched.MeanError);

        public PracticeResult? Result(DateTimeOffset dateAtTimeZero)
        {
            if (_bothPlayingSeconds < MinimumMixSeconds) return null;
            var secondsToMatch = _matchedAt - _start;
            var mix = new PracticeMix(_id, dateAtTimeZero.AddSeconds(_start), dateAtTimeZero.AddSeconds(LastBothPlaying), Outgoing, Incoming,
                _bothPlayingSeconds, secondsToMatch, _matched.MeanTempoDifference, _matched.MeanError, _matched.WorstError);
            return new PracticeResult(mix, new PracticeTrace(_id, LastBothPlaying - _start, secondsToMatch, _trace.ToValueList()));
        }

        private void UpdateMatch(double time)
        {
            if (_tempoDifference is not { } difference) return;
            try
            {
                if (!(Math.Abs(difference) < MatchToleranceBPM))
                {
                    _candidateStart = null;
                    _pending = new MatchedStats();
                    return;
                }
                if (_candidateStart is not { } candidateStart || _lastTempoSample is not { } lastTempoSample || time - lastTempoSample > MaximumSampleInterval)
                {
                    _candidateStart = time;
                    _pending = new MatchedStats();
                    return;
                }
                if (time - candidateStart >= MatchHoldSeconds)
                {
                    _matchedAt = Math.Max(_start, candidateStart - PracticeDeckTempo.Window / 2);
                    _matched = _pending;
                    _pending = new MatchedStats();
                }
            }
            finally
            {
                _lastTempoSample = time;
            }
        }

        private void AppendTrace(double time)
        {
            if (_phaseError is not { } error) return;
            var seconds = time - _start;
            if (_trace.Count > 0 && seconds - _trace[^1].Seconds < _traceInterval - 0.004) return;
            _trace.Add(new PracticeTraceSample(Rounded(seconds, 3), Rounded(error, 2)));
            if (_trace.Count < TraceCapacity) return;
            var kept = _trace.Where((_, index) => index % 2 == 0).ToList();
            _trace.Clear();
            _trace.AddRange(kept);
            _traceInterval *= 2;
        }

        private static double? PhaseError(LiveDeckSnapshot outgoing, LiveDeckSnapshot incoming, double slaveBPM)
        {
            if (outgoing.PhaseInBar is not { } outgoingPhase || incoming.PhaseInBar is not { } incomingPhase || !(slaveBPM > 3)) return null;
            var beats = incomingPhase - outgoingPhase;
            var wrapped = beats - Math.Floor(beats + 0.5);
            return wrapped * 60_000 / slaveBPM;
        }

        private static double Rounded(double value, int places)
        {
            var scale = Math.Pow(10, places);
            return Numbers.Round(value * scale) / scale;
        }
    }

    private sealed class MatchedStats
    {
        private double _errorSum;
        private int _errorCount;
        private double _tempoSum;
        private int _tempoCount;

        public double? WorstError { get; private set; }

        public void Add(double? error, double? tempoDifference)
        {
            if (error is { } value)
            {
                _errorSum += Math.Abs(value);
                _errorCount++;
                WorstError = Math.Max(WorstError ?? 0, Math.Abs(value));
            }
            if (tempoDifference is { } difference)
            {
                _tempoSum += difference;
                _tempoCount++;
            }
        }

        public double? MeanError => _errorCount > 0 ? _errorSum / _errorCount : null;
        public double? MeanTempoDifference => _tempoCount > 0 ? _tempoSum / _tempoCount : null;
    }
}
