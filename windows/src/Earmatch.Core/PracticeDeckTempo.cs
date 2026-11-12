namespace Earmatch.Core;

/// A deck's tempo for the mix trainer: the slope of its unwrapped beats over the last two seconds of play. A jump the
/// deck's own tempo cannot explain (a seek, a lost frame) starts the measurement over.
internal sealed class PracticeDeckTempo
{
    internal const double Window = 2;
    internal const double MinimumSpan = 1;
    internal const int MinimumSamples = 8;
    internal const double JumpTolerance = 0.25;

    private readonly record struct Sample(double Time, double Beats);

    private readonly List<Sample> _samples = [];
    private double? _lastPhase;

    /// When the deck started playing, on the session's clock; none while it is stopped.
    public double? PlayingSince { get; private set; }

    public void Add(LiveDeckSnapshot deck, double time)
    {
        if (deck.IsPlaying) PlayingSince ??= time;
        else if (deck.PhaseInBar is not null) PlayingSince = null;
        if (!deck.IsPlaying || deck.PhaseInBar is not { } phase)
        {
            Reset();
            return;
        }
        try
        {
            if (_samples.Count == 0 || _lastPhase is not { } lastPhase)
            {
                _samples.Add(new Sample(time, phase));
                return;
            }
            var last = _samples[^1];
            var elapsed = time - last.Time;
            if (!(elapsed > 0)) return;
            var step = PhaseTracker.CircularBeats(phase - lastPhase);
            if (elapsed > PracticeSession.MaximumSampleInterval || Math.Abs(step - deck.BeatsPerSecond * elapsed) > JumpTolerance)
            {
                _samples.Clear();
                _samples.Add(new Sample(time, phase));
                return;
            }
            _samples.Add(new Sample(time, last.Beats + step));
            var firstKept = _samples.FindIndex(sample => time - sample.Time <= Window);
            if (firstKept > 0) _samples.RemoveRange(0, firstKept);
        }
        finally
        {
            _lastPhase = phase;
        }
    }

    /// The least-squares tempo, once at least eight samples span a second.
    public double? BeatsPerMinute
    {
        get
        {
            if (_samples.Count < MinimumSamples || !(_samples[^1].Time - _samples[0].Time >= MinimumSpan)) return null;
            var first = _samples[0];
            double count = _samples.Count, meanTime = 0, meanBeats = 0;
            foreach (var sample in _samples)
            {
                meanTime += sample.Time - first.Time;
                meanBeats += sample.Beats;
            }
            meanTime /= count;
            meanBeats /= count;
            double covariance = 0, variance = 0;
            foreach (var sample in _samples)
            {
                var time = sample.Time - first.Time - meanTime;
                covariance += time * (sample.Beats - meanBeats);
                variance += time * time;
            }
            if (!(variance > 0)) return null;
            return covariance / variance * 60;
        }
    }

    private void Reset()
    {
        _samples.Clear();
        _lastPhase = null;
    }
}
