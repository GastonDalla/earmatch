import Foundation

public struct CaptureDiagnostics: Equatable, Sendable {
    public struct Deck: Equatable, Sendable {
        public var number: Int
        public var isLoaded: Bool
        public var reading: String
        public var beatSpacing: Double?
        public var phaseInBar: Double?
        public var beatsPerSecond: Double?
        public var isMaster: Bool
        public var headerBPM: Double?
        public var elapsedSeconds: Double?

        public init(number: Int, isLoaded: Bool, reading: String, beatSpacing: Double?, phaseInBar: Double?,
                    beatsPerSecond: Double?, isMaster: Bool, headerBPM: Double?, elapsedSeconds: Double?) {
            self.number = number
            self.isLoaded = isLoaded
            self.reading = reading
            self.beatSpacing = beatSpacing
            self.phaseInBar = phaseInBar
            self.beatsPerSecond = beatsPerSecond
            self.isMaster = isMaster
            self.headerBPM = headerBPM
            self.elapsedSeconds = elapsedSeconds
        }
    }

    public var earmatchVersion: String
    public var macOSVersion: String
    public var rekordboxVersion: String?
    public var accessibilityGranted: Bool
    public var screenRecordingGranted: Bool
    public var activation: String
    public var layout: String?
    public var capture: String
    public var mappingProblem: String?
    public var waveformFound: Bool
    public var framesPerSecond: Double
    public var captureJitterMilliseconds: Double
    public var analysisMilliseconds: Double
    public var maxAnalysisMilliseconds: Double
    public var earliestLatencyMilliseconds: Double
    public var latencyMilliseconds: Double
    public var maxLatencyMilliseconds: Double
    public var refreshesPerSecond: Double
    public var meterErrorMilliseconds: Double?
    public var decks: [Deck]

    public init(earmatchVersion: String, macOSVersion: String, rekordboxVersion: String?, accessibilityGranted: Bool,
                screenRecordingGranted: Bool, activation: String, layout: String?, capture: String, mappingProblem: String?,
                waveformFound: Bool, framesPerSecond: Double, captureJitterMilliseconds: Double, analysisMilliseconds: Double,
                maxAnalysisMilliseconds: Double, earliestLatencyMilliseconds: Double, latencyMilliseconds: Double,
                maxLatencyMilliseconds: Double, refreshesPerSecond: Double, meterErrorMilliseconds: Double?, decks: [Deck]) {
        self.earmatchVersion = earmatchVersion
        self.macOSVersion = macOSVersion
        self.rekordboxVersion = rekordboxVersion
        self.accessibilityGranted = accessibilityGranted
        self.screenRecordingGranted = screenRecordingGranted
        self.activation = activation
        self.layout = layout
        self.capture = capture
        self.mappingProblem = mappingProblem
        self.waveformFound = waveformFound
        self.framesPerSecond = framesPerSecond
        self.captureJitterMilliseconds = captureJitterMilliseconds
        self.analysisMilliseconds = analysisMilliseconds
        self.maxAnalysisMilliseconds = maxAnalysisMilliseconds
        self.earliestLatencyMilliseconds = earliestLatencyMilliseconds
        self.latencyMilliseconds = latencyMilliseconds
        self.maxLatencyMilliseconds = maxLatencyMilliseconds
        self.refreshesPerSecond = refreshesPerSecond
        self.meterErrorMilliseconds = meterErrorMilliseconds
        self.decks = decks
    }

    public var text: String {
        func granted(_ value: Bool) -> String { value ? "granted" : "missing" }
        func number(_ format: String, _ value: Double) -> String { String(format: format, value) }
        let capture = mappingProblem.map { "\(self.capture) (\($0))" } ?? self.capture
        let frames = String(format: "Frames: %.1f fps (interval sd %.2f ms), analysis %.2f ms avg / %.2f ms max",
                            framesPerSecond, captureJitterMilliseconds, analysisMilliseconds, maxAnalysisMilliseconds)
        let latency = String(format: "Latency: capture to screen %.1f to %.1f ms avg, %.1f ms max; meter drawn %.0f times/s, predicted for the screen",
                             earliestLatencyMilliseconds, latencyMilliseconds, maxLatencyMilliseconds, refreshesPerSecond)
        var lines = [
            "Earmatch \(earmatchVersion), macOS \(macOSVersion), rekordbox \(rekordboxVersion ?? "not running")",
            "Accessibility: \(granted(accessibilityGranted)); Screen Recording: \(granted(screenRecordingGranted))",
            "Activation: \(activation)",
            "Layout: \(layout ?? "-")",
            "Capture: \(capture)",
            waveformFound ? "Stacked waveform: found" : "Stacked waveform: not found",
            frames,
            latency + (meterErrorMilliseconds.map { number(" (%.1f ms from rekordbox)", $0) } ?? ""),
        ]
        for deck in decks {
            let none = "-"
            let spacing = deck.beatSpacing.map { number("%.2f pt/beat", $0) } ?? none
            let phase = deck.phaseInBar.map { number("%.2f", $0) } ?? none
            let tempo = deck.beatsPerSecond.map { number("%.1f BPM", $0 * 60) } ?? none
            let bpm = deck.headerBPM.map { number("%.2f", $0) } ?? none
            let elapsed = deck.elapsedSeconds.map { number("%.1f s", $0) } ?? none
            lines.append("Deck \(deck.number): \(deck.isLoaded ? "loaded" : "empty"), grid \(deck.reading), spacing \(spacing), "
                + "phase \(phase), playing \(tempo), master \(deck.isMaster ? "yes" : "no"), BPM \(bpm), elapsed \(elapsed)")
        }
        return lines.joined(separator: "\n")
    }
}
