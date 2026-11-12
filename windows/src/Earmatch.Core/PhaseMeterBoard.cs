namespace Earmatch.Core;

public sealed record FrameMappingProblem(int FrameWidth, int FrameHeight, int WindowWidth, int WindowHeight)
{
    public const double SizeTolerance = 2;

    public string Message => L10n.Format("frame shows %ldx%ld px but the window is %ldx%ld px", FrameWidth, FrameHeight, WindowWidth, WindowHeight);

    public static FrameMappingProblem? Check(int frameWidth, int frameHeight, ScreenSize window) =>
        Math.Abs(frameWidth - window.Width) <= SizeTolerance && Math.Abs(frameHeight - window.Height) <= SizeTolerance
            ? null
            : new FrameMappingProblem(frameWidth, frameHeight, (int)Math.Round(window.Width), (int)Math.Round(window.Height));
}

public sealed record FrameAnalysis(IReadOnlyDictionary<int, DeckFrameReading> Decks, FrameMappingProblem? MappingProblem, double Time);

public sealed class PhaseMeterBoard
{
    public const int UnmappedFramesTolerated = 6;

    private readonly FrameStats? _tracedStats;
    private readonly Dictionary<int, DrawnHistory> _drawn = [];
    private readonly Dictionary<int, PhasePredictor> _predictors = [];
    private readonly Dictionary<int, PhaseMeterDisplay?> _displays = [];
    private List<PhaseMeterDeckInput> _inputs = [];
    private List<PhaseMeterDeckInput> _previousInputs = [];
    private int? _previousMaster;
    private bool _previousHadFrame;
    private bool _isStale = true;
    private int _unmappedFrames;

    public PhaseMeterBoard(FrameStats? stats = null)
    {
        _tracedStats = stats is { IsTracing: true } ? stats : null;
    }

    public FrameAnalysis? Latest { get; private set; }

    public int? Master { get; private set; }

    public bool HasFrame => Latest is { MappingProblem: null };

    public bool Accept(FrameAnalysis frame, RekordboxLayout layout, out bool problemChanged)
    {
        problemChanged = false;
        if (_tracedStats is not null) MeasureMeterError(frame, _tracedStats);
        if (frame.MappingProblem is not null && Latest is { MappingProblem: null } && _unmappedFrames < UnmappedFramesTolerated)
        {
            _unmappedFrames++;
            return false;
        }
        foreach (var (number, deck) in frame.Decks)
        {
            if (!_predictors.TryGetValue(number, out var predictor)) _predictors[number] = predictor = new PhasePredictor();
            predictor.Update(new PhaseAnchor(frame.Time, deck.Phase.PhaseInBar, deck.BeatsPerSecond, deck.Phase.Status == PhaseTrackerStatus.Live));
        }
        _unmappedFrames = frame.MappingProblem is null ? 0 : _unmappedFrames;
        problemChanged = frame.MappingProblem != Latest?.MappingProblem;
        Latest = frame;
        Master = layout.Decks.FirstOrDefault(deck => frame.Decks.TryGetValue(deck.Number, out var reading) && reading.IsMaster)?.Number;
        return true;
    }

    public bool Reset()
    {
        var problemCleared = Latest?.MappingProblem is not null;
        Latest = null;
        Master = null;
        _unmappedFrames = 0;
        _predictors.Clear();
        _drawn.Clear();
        _displays.Clear();
        Invalidate();
        return problemCleared;
    }

    public void Invalidate()
    {
        _isStale = true;
        _previousInputs.Clear();
    }

    public bool Compute(RekordboxLayout layout, OverlaySettings settings, double time)
    {
        var hasFrame = HasFrame;
        (_inputs, _previousInputs) = (_previousInputs, _inputs);
        _inputs.Clear();
        foreach (var deck in layout.Decks)
        {
            var predictor = _predictors.GetValueOrDefault(deck.Number);
            var phase = hasFrame ? predictor?.Phase(time) : null;
            if (_tracedStats is not null && phase is { } drawn)
            {
                if (!_drawn.TryGetValue(deck.Number, out var history)) _drawn[deck.Number] = history = new DrawnHistory();
                history.Append(time, drawn);
            }
            _inputs.Add(new PhaseMeterDeckInput(deck.Number, phase, Counter(deck, phase, hasFrame, settings, predictor, time)));
        }
        if (!_isStale && _inputs.SequenceEqual(_previousInputs) && Master == _previousMaster && hasFrame == _previousHadFrame) return false;
        _isStale = false;
        _previousMaster = Master;
        _previousHadFrame = hasFrame;
        _displays.Clear();
        foreach (var deck in layout.Decks)
        {
            _displays[deck.Number] = hasFrame && IsLoaded(deck) ? PhaseMeterModel.Display(deck.Number, _inputs, Master, settings.PhaseMeterType) : null;
        }
        return true;
    }

    public PhaseMeterDisplay? Display(int deck) => _displays.GetValueOrDefault(deck);

    public LiveDecksSnapshot LiveSnapshot(RekordboxLayout layout, double framesPerSecond, double latencyMilliseconds, double now)
    {
        var shown = layout.Decks.Count > 2 ? layout.Decks.Where(IsLoaded) : layout.Decks;
        var decks = shown.Select(deck =>
        {
            var predictor = _predictors.GetValueOrDefault(deck.Number);
            return new LiveDeckSnapshot(deck.Number, deck.Title, Master == deck.Number, Display(deck.Number), predictor?.PredictedPhase(now),
                predictor?.BeatsPerSecond ?? 0);
        }).ToValueList();
        return new LiveDecksSnapshot(decks, framesPerSecond, latencyMilliseconds);
    }

    public double BeatsPerSecond(int deck) => _predictors.GetValueOrDefault(deck)?.BeatsPerSecond ?? 0;

    private BarPosition? Counter(DeckLayout deck, double? phase, bool hasFrame, OverlaySettings settings, PhasePredictor? predictor, double time)
    {
        var latest = hasFrame ? Latest : null;
        var cue = latest is not null && latest.Decks.TryGetValue(deck.Number, out var reading) ? reading.CueBeatsAhead : null;
        return PhaseMeterCounter.Position(settings.CounterStyle, deck, phase, cue, latest?.Time ?? time, predictor?.BeatsPerSecond ?? 0, time);
    }

    public static bool IsLoaded(DeckLayout deck) => deck.BPM is not null || deck.Title is not null;

    private void MeasureMeterError(FrameAnalysis frame, FrameStats stats)
    {
        foreach (var (number, deck) in frame.Decks)
        {
            if (deck.Phase.Status != PhaseTrackerStatus.Live || deck.Phase.PhaseInBar is not { } measured || deck.BeatsPerSecond is not { } rate
                || Math.Abs(rate) <= 0.5 || !_drawn.TryGetValue(number, out var history) || history.Phase(frame.Time, rate) is not { } drawn)
                continue;
            var difference = (measured - drawn + 2) % 4;
            var beats = (difference < 0 ? difference + 4 : difference) - 2;
            stats.RecordPredictionError(beats / rate);
        }
    }

    private sealed class DrawnHistory
    {
        private const int Count = 16;
        private const double MaxDistance = 0.0084;
        private readonly double[] _times = Enumerable.Repeat(double.NegativeInfinity, Count).ToArray();
        private readonly double[] _phases = new double[Count];
        private int _next;

        public void Append(double time, double phase)
        {
            _times[_next] = time;
            _phases[_next] = phase;
            _next = (_next + 1) % Count;
        }

        public double? Phase(double time, double beatsPerSecond)
        {
            var best = 0;
            for (var index = 1; index < Count; index++)
            {
                if (Math.Abs(_times[index] - time) < Math.Abs(_times[best] - time)) best = index;
            }
            if (!(Math.Abs(_times[best] - time) <= MaxDistance)) return null;
            return _phases[best] + beatsPerSecond * (time - _times[best]);
        }
    }
}
