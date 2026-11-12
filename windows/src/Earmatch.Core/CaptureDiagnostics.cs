using System.Globalization;

namespace Earmatch.Core;

public sealed record CaptureDiagnostics
{
    public sealed record Deck
    {
        public Deck(int number, bool isLoaded, string reading, double? beatSpacing, double? phaseInBar, double? beatsPerSecond,
            bool isMaster, double? headerBPM, double? elapsedSeconds)
        {
            Number = number;
            IsLoaded = isLoaded;
            Reading = reading;
            BeatSpacing = beatSpacing;
            PhaseInBar = phaseInBar;
            BeatsPerSecond = beatsPerSecond;
            IsMaster = isMaster;
            HeaderBPM = headerBPM;
            ElapsedSeconds = elapsedSeconds;
        }

        public int Number { get; init; }
        public bool IsLoaded { get; init; }
        public string Reading { get; init; }
        public double? BeatSpacing { get; init; }
        public double? PhaseInBar { get; init; }
        public double? BeatsPerSecond { get; init; }
        public bool IsMaster { get; init; }
        public double? HeaderBPM { get; init; }
        public double? ElapsedSeconds { get; init; }
    }

    public CaptureDiagnostics(string earmatchVersion, string windowsVersion, string? rekordboxVersion, string activation, string? layout,
        string capture, string? mappingProblem, bool waveformFound, double framesPerSecond, double captureJitterMilliseconds,
        double analysisMilliseconds, double maxAnalysisMilliseconds, double earliestLatencyMilliseconds, double latencyMilliseconds,
        double maxLatencyMilliseconds, double refreshesPerSecond, double? meterErrorMilliseconds, IReadOnlyList<Deck> decks)
    {
        EarmatchVersion = earmatchVersion;
        WindowsVersion = windowsVersion;
        RekordboxVersion = rekordboxVersion;
        Activation = activation;
        Layout = layout;
        Capture = capture;
        MappingProblem = mappingProblem;
        WaveformFound = waveformFound;
        FramesPerSecond = framesPerSecond;
        CaptureJitterMilliseconds = captureJitterMilliseconds;
        AnalysisMilliseconds = analysisMilliseconds;
        MaxAnalysisMilliseconds = maxAnalysisMilliseconds;
        EarliestLatencyMilliseconds = earliestLatencyMilliseconds;
        LatencyMilliseconds = latencyMilliseconds;
        MaxLatencyMilliseconds = maxLatencyMilliseconds;
        RefreshesPerSecond = refreshesPerSecond;
        MeterErrorMilliseconds = meterErrorMilliseconds;
        Decks = decks;
    }

    public string EarmatchVersion { get; init; }
    public string WindowsVersion { get; init; }
    public string? RekordboxVersion { get; init; }
    public string Activation { get; init; }
    public string? Layout { get; init; }
    public string Capture { get; init; }
    public string? MappingProblem { get; init; }
    public bool WaveformFound { get; init; }
    public double FramesPerSecond { get; init; }
    public double CaptureJitterMilliseconds { get; init; }
    public double AnalysisMilliseconds { get; init; }
    public double MaxAnalysisMilliseconds { get; init; }
    public double EarliestLatencyMilliseconds { get; init; }
    public double LatencyMilliseconds { get; init; }
    public double MaxLatencyMilliseconds { get; init; }
    public double RefreshesPerSecond { get; init; }
    public double? MeterErrorMilliseconds { get; init; }
    public IReadOnlyList<Deck> Decks { get; init; }

    public string Text
    {
        get
        {
            static string Number(string format, double value) => value.ToString(format, CultureInfo.InvariantCulture);
            var capture = MappingProblem is { } problem ? $"{Capture} ({problem})" : Capture;
            var frames = $"Frames: {Number("F1", FramesPerSecond)} fps (interval sd {Number("F2", CaptureJitterMilliseconds)} ms), "
                + $"analysis {Number("F2", AnalysisMilliseconds)} ms avg / {Number("F2", MaxAnalysisMilliseconds)} ms max";
            var latency = $"Latency: capture to screen {Number("F1", EarliestLatencyMilliseconds)} to {Number("F1", LatencyMilliseconds)} ms avg, "
                + $"{Number("F1", MaxLatencyMilliseconds)} ms max; meter drawn {Number("F0", RefreshesPerSecond)} times/s, predicted for the screen";
            var lines = new List<string>
            {
                $"Earmatch {EarmatchVersion}, Windows {WindowsVersion}, rekordbox {RekordboxVersion ?? "not running"}",
                $"Activation: {Activation}",
                $"Layout: {Layout ?? "-"}",
                $"Capture: {capture}",
                WaveformFound ? "Stacked waveform: found" : "Stacked waveform: not found",
                frames,
                latency + (MeterErrorMilliseconds is { } error ? $" ({Number("F1", error)} ms from rekordbox)" : ""),
            };
            foreach (var deck in Decks)
            {
                const string none = "-";
                var spacing = deck.BeatSpacing is { } s ? $"{Number("F2", s)} px/beat" : none;
                var phase = deck.PhaseInBar is { } p ? Number("F2", p) : none;
                var tempo = deck.BeatsPerSecond is { } b ? $"{Number("F1", b * 60)} BPM" : none;
                var bpm = deck.HeaderBPM is { } h ? Number("F2", h) : none;
                var elapsed = deck.ElapsedSeconds is { } e ? $"{Number("F1", e)} s" : none;
                lines.Add($"Deck {deck.Number}: {(deck.IsLoaded ? "loaded" : "empty")}, grid {deck.Reading}, spacing {spacing}, "
                    + $"phase {phase}, playing {tempo}, master {(deck.IsMaster ? "yes" : "no")}, BPM {bpm}, elapsed {elapsed}");
            }
            return string.Join("\n", lines);
        }
    }
}
