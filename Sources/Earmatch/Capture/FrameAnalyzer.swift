import CoreMedia
import CoreVideo
import EarmatchCore
import Foundation
import QuartzCore
import ScreenCaptureKit

struct BPMCovers: Equatable {
    var oneDecimal: CGRect
    var noDecimals: CGRect

    func cover(for limit: BPMLimit) -> CGRect? {
        switch limit {
        case .oneDecimal: oneDecimal
        case .noDecimals: noDecimals
        case .coverFully: nil
        }
    }
}

struct DeckFrameReading: Equatable {
    var grid: BeatGridReading?
    var phase: PhaseTracker.Estimate
    var beatsPerSecond: Double?
    var isMaster: Bool
    var bpmCovers: BPMCovers?
    var cueBeatsAhead: Double?
}

struct FrameReading {
    let pixelBuffer: CVPixelBuffer
    let pixelSize: CGSize
    let scale: CGFloat
    let origin: CGPoint
    let windowScreenRect: CGRect?
    let decks: [Int: DeckFrameReading]
    let mappingProblem: FrameMappingProblem?
    let time: Double
    let generation: Int

    var surface: IOSurfaceRef? { CVPixelBufferGetIOSurface(pixelBuffer)?.takeUnretainedValue() }

    func pixelRect(_ windowRect: CGRect) -> CGRect {
        CGRect(x: (windowRect.minX - origin.x) * scale, y: (windowRect.minY - origin.y) * scale,
               width: windowRect.width * scale, height: windowRect.height * scale)
    }
}

final class FrameAnalyzer {
    struct Input: Equatable {
        var decks: [DeckLayout]
        var windowSize: CGSize
        var measuresBPMGlyphs: Bool
        var readsCues: Bool
    }

    let stats = FrameStats()
    private let lock = NSLock()
    private var input: Input?
    private var trackers: [Int: PhaseTracker] = [:]
    private var playheads: [Int: (strip: CGRect, position: CGFloat)] = [:]

    func configure(_ newInput: Input?) {
        lock.lock()
        input = newInput
        lock.unlock()
    }

    func analyze(_ sampleBuffer: CMSampleBuffer, generation: Int) -> FrameReading? {
        let started = CACurrentMediaTime()
        lock.lock()
        let input = self.input
        lock.unlock()
        guard let input, let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer),
              CVPixelBufferGetPixelFormatType(pixelBuffer) == kCVPixelFormatType_32BGRA
        else { return nil }

        let info = FrameInfo(sampleBuffer)
        let time = info.displayTime ?? CMSampleBufferGetPresentationTimeStamp(sampleBuffer).seconds
        let width = CVPixelBufferGetWidth(pixelBuffer), height = CVPixelBufferGetHeight(pixelBuffer)
        let mappingProblem = info.mappingProblem(windowSize: input.windowSize)

        CVPixelBufferLockBaseAddress(pixelBuffer, .readOnly)
        defer { CVPixelBufferUnlockBaseAddress(pixelBuffer, .readOnly) }
        var decks: [Int: DeckFrameReading] = [:]
        if let base = CVPixelBufferGetBaseAddress(pixelBuffer) {
            let image = PixelImage(base: UnsafeRawPointer(base), width: width, height: height,
                                   bytesPerRow: CVPixelBufferGetBytesPerRow(pixelBuffer), layout: .bgra,
                                   scale: info.scale, origin: info.origin)
            for deck in input.decks {
                decks[deck.number] = read(deck, in: image, mapped: mappingProblem == nil, input: input, time: time)
            }
        }
        stats.record(processingSeconds: CACurrentMediaTime() - started, frameTime: time, decks: decks)
        return FrameReading(
            pixelBuffer: pixelBuffer, pixelSize: CGSize(width: width, height: height), scale: info.scale, origin: info.origin,
            windowScreenRect: info.screenRect, decks: decks, mappingProblem: mappingProblem, time: time, generation: generation
        )
    }

    private func read(_ deck: DeckLayout, in image: PixelImage, mapped: Bool, input: Input, time: Double) -> DeckFrameReading {
        let measuresGlyphs = input.measuresBPMGlyphs
        let isLoaded = deck.bpm != nil || deck.title != nil
        let playhead = mapped && isLoaded ? deck.strip.map { playhead(of: deck.number, strip: $0, in: image) } : nil
        let grid: BeatGridReading? = if let strip = deck.strip, let playhead {
            BeatGridDetector.read(strip: strip, playhead: playhead, in: image)
        } else {
            nil
        }
        var tracker = trackers[deck.number] ?? PhaseTracker()
        let phase = tracker.update(grid, at: time)
        trackers[deck.number] = tracker
        let covers: BPMCovers? = measuresGlyphs ? deck.jogBPMFrame.map { jog in
            guard mapped else { return BPMCovers(oneDecimal: jog, noDecimals: jog) }
            return BPMCovers(oneDecimal: BPMGlyphMask.cover(.oneDecimal, jogFrame: jog, image: image),
                             noDecimals: BPMGlyphMask.cover(.noDecimals, jogFrame: jog, image: image))
        } : nil
        let cues: Double? = if input.readsCues, let strip = deck.strip, strip.orientation == .horizontal, let playhead,
                               let spacing = phase.beatSpacing {
            CueCountdown.beatsToNextCue(strip: strip.frame, playheadX: playhead, beatSpacing: spacing, in: image)
        } else {
            nil
        }
        return DeckFrameReading(
            grid: grid,
            phase: phase,
            beatsPerSecond: tracker.beatsPerSecond,
            isMaster: mapped && SyncMasterDetector.isMaster(button: deck.masterButtonFrame, in: image),
            bpmCovers: covers,
            cueBeatsAhead: cues
        )
    }
}

extension FrameAnalyzer {
    fileprivate func playhead(of deck: Int, strip: WaveformStrip, in image: PixelImage) -> CGFloat {
        if let found = PlayheadDetector.locate(strip, in: image) {
            playheads[deck] = (strip.frame, found)
            return found
        }
        if let known = playheads[deck], known.strip == strip.frame { return known.position }
        return strip.center + 0.5
    }
}

private struct FrameInfo {
    var contentRect: CGRect?
    var contentScale: CGFloat = 1
    var scaleFactor: CGFloat = 2
    var screenRect: CGRect?
    var displayTime: Double?

    private static let secondsPerMachTick: Double = {
        var timebase = mach_timebase_info_data_t()
        mach_timebase_info(&timebase)
        return Double(timebase.numer) / Double(timebase.denom) / 1_000_000_000
    }()

    init(_ sampleBuffer: CMSampleBuffer) {
        guard let attachments = (CMSampleBufferGetSampleAttachmentsArray(sampleBuffer, createIfNecessary: false) as? [[SCStreamFrameInfo: Any]])?.first
        else { return }
        if let ticks = attachments[.displayTime] as? UInt64, ticks > 0 { displayTime = Double(ticks) * Self.secondsPerMachTick }
        contentRect = (attachments[.contentRect] as! CFDictionary?).flatMap { CGRect(dictionaryRepresentation: $0) }
        screenRect = (attachments[.screenRect] as! CFDictionary?).flatMap { CGRect(dictionaryRepresentation: $0) }
        if let value = attachments[.contentScale] as? CGFloat, value > 0 { contentScale = value }
        if let value = attachments[.scaleFactor] as? CGFloat, value > 0 { scaleFactor = value }
    }

    var scale: CGFloat { scaleFactor * contentScale }

    var origin: CGPoint {
        guard let contentRect else { return .zero }
        return CGPoint(x: -contentRect.minX / contentScale, y: -contentRect.minY / contentScale)
    }

    func mappingProblem(windowSize: CGSize) -> FrameMappingProblem? {
        guard let contentRect else { return .noContentRect }
        let size = CGSize(width: contentRect.width / contentScale, height: contentRect.height / contentScale)
        guard abs(size.width - windowSize.width) <= 2, abs(size.height - windowSize.height) <= 2 else {
            return .sizeMismatch(frameWidth: Int(size.width), frameHeight: Int(size.height),
                                 windowWidth: Int(windowSize.width), windowHeight: Int(windowSize.height))
        }
        return nil
    }
}

enum FrameMappingProblem: Equatable {
    case noContentRect
    case sizeMismatch(frameWidth: Int, frameHeight: Int, windowWidth: Int, windowHeight: Int)

    var message: String {
        switch self {
        case .noContentRect:
            L10n.text("frame has no content rect")
        case let .sizeMismatch(frameWidth, frameHeight, windowWidth, windowHeight):
            L10n.format("frame shows %ldx%ld pt but the window is %ldx%ld pt", frameWidth, frameHeight, windowWidth, windowHeight)
        }
    }
}

final class FrameStats {
    struct Snapshot: Equatable {
        var framesPerSecond: Double = 0
        var averageProcessingMilliseconds: Double = 0
        var maxProcessingMilliseconds: Double = 0
        var averageLatencyMilliseconds: Double = 0
        var maxLatencyMilliseconds: Double = 0
        var averageEarliestLatencyMilliseconds: Double = 0
        var averageCommitMilliseconds: Double = 0
        var captureJitterMilliseconds: Double = 0
        var refreshesPerSecond: Double = 0
        var repeatedFramesPerSecond: Double = 0
        var averageRefreshMicroseconds: Double = 0
        var maxRefreshMicroseconds: Double = 0
        var predictionErrorMilliseconds: Double?
        var deckStatus: [Int: PhaseTracker.Status] = [:]
    }

    static let isTracing = ProcessInfo.processInfo.environment["EARMATCH_TRACE"] == "1"

    private let lock = NSLock()
    private var windowStart: Double?
    private var frames = 0
    private var totalProcessing = 0.0
    private var maxProcessing = 0.0
    private var lastFrameTime: Double?
    private var intervals = 0
    private var intervalSum = 0.0
    private var intervalSquares = 0.0
    private var totalLatency = 0.0
    private var totalEarliestLatency = 0.0
    private var maxLatency = 0.0
    private var latencySamples = 0
    private var totalCommit = 0.0
    private var commitSamples = 0
    private var repeatedFrames = 0
    private var refreshes = 0
    private var totalRefreshWork = 0.0
    private var maxRefreshWork = 0.0
    private var totalPredictionError = 0.0
    private var predictionSamples = 0
    private var heldFrames: [Int: Int] = [:]
    private var lostFrames: [Int: Int] = [:]
    private var last = Snapshot()

    var snapshot: Snapshot {
        lock.lock()
        defer { lock.unlock() }
        return last
    }

    func recordCommit(seconds: Double) {
        lock.lock()
        totalCommit += seconds
        commitSamples += 1
        lock.unlock()
    }

    func recordScreen(earliest: Double, latest: Double) {
        lock.lock()
        totalEarliestLatency += earliest
        totalLatency += latest
        maxLatency = max(maxLatency, latest)
        latencySamples += 1
        lock.unlock()
    }

    func recordRepeatedFrame() {
        lock.lock()
        repeatedFrames += 1
        lock.unlock()
    }

    func recordRefresh(workSeconds: Double) {
        lock.lock()
        refreshes += 1
        totalRefreshWork += workSeconds
        maxRefreshWork = max(maxRefreshWork, workSeconds)
        lock.unlock()
    }

    func recordPredictionError(seconds: Double) {
        lock.lock()
        totalPredictionError += abs(seconds)
        predictionSamples += 1
        lock.unlock()
    }

    func record(processingSeconds: Double, frameTime: Double, decks: [Int: DeckFrameReading]) {
        lock.lock()
        defer { lock.unlock() }
        frames += 1
        totalProcessing += processingSeconds
        maxProcessing = max(maxProcessing, processingSeconds)
        if let previous = lastFrameTime, frameTime > previous {
            let interval = frameTime - previous
            intervals += 1
            intervalSum += interval
            intervalSquares += interval * interval
        }
        lastFrameTime = frameTime
        last.deckStatus = decks.mapValues(\.phase.status)
        for (number, deck) in decks {
            if deck.phase.status == .held { heldFrames[number, default: 0] += 1 }
            if deck.phase.status == .lost { lostFrames[number, default: 0] += 1 }
        }
        guard let start = windowStart else {
            windowStart = frameTime
            return
        }
        let elapsed = frameTime - start
        guard elapsed >= 1 else { return }
        last.framesPerSecond = Double(frames - 1) / elapsed
        last.averageProcessingMilliseconds = totalProcessing / Double(frames) * 1000
        last.maxProcessingMilliseconds = maxProcessing * 1000
        last.averageLatencyMilliseconds = latencySamples > 0 ? totalLatency / Double(latencySamples) * 1000 : 0
        last.averageEarliestLatencyMilliseconds = latencySamples > 0 ? totalEarliestLatency / Double(latencySamples) * 1000 : 0
        last.maxLatencyMilliseconds = maxLatency * 1000
        last.averageCommitMilliseconds = commitSamples > 0 ? totalCommit / Double(commitSamples) * 1000 : 0
        last.repeatedFramesPerSecond = Double(repeatedFrames) / elapsed
        if intervals > 1 {
            let mean = intervalSum / Double(intervals)
            last.captureJitterMilliseconds = max(0, intervalSquares / Double(intervals) - mean * mean).squareRoot() * 1000
        }
        last.refreshesPerSecond = Double(refreshes) / elapsed
        last.averageRefreshMicroseconds = refreshes > 0 ? totalRefreshWork / Double(refreshes) * 1_000_000 : 0
        last.maxRefreshMicroseconds = maxRefreshWork * 1_000_000
        last.predictionErrorMilliseconds = predictionSamples > 0 ? totalPredictionError / Double(predictionSamples) * 1000 : nil
        if Self.isTracing {
            let decksText = decks.keys.sorted().map { number -> String in
                let deck = decks[number]!
                let phase = deck.phase.phaseInBar.map { String(format: "%.2f", $0) } ?? "-"
                let rate = deck.beatsPerSecond.map { String(format: "%.3f", $0) } ?? "-"
                return "deck\(number)=\(deck.phase.status) phase=\(phase) bps=\(rate) master=\(deck.isMaster) held=\(heldFrames[number] ?? 0) lost=\(lostFrames[number] ?? 0)"
            }.joined(separator: " ")
            let prediction = last.predictionErrorMilliseconds.map { String(format: "%.1f ms", $0) } ?? "-"
            let line = String(format: "earmatch: %.1f fps (interval sd %.2f ms), analysis avg %.2f ms max %.2f ms, capture-to-commit %.1f ms, capture-to-screen %.1f..%.1f ms (max %.1f), refreshes %.0f/s (no new frame %.0f/s, meters on every one) work avg %.0f us max %.0f us, meter error ",
                              last.framesPerSecond, last.captureJitterMilliseconds, last.averageProcessingMilliseconds,
                              last.maxProcessingMilliseconds, last.averageCommitMilliseconds, last.averageEarliestLatencyMilliseconds,
                              last.averageLatencyMilliseconds, last.maxLatencyMilliseconds, last.refreshesPerSecond,
                              last.repeatedFramesPerSecond, last.averageRefreshMicroseconds,
                              last.maxRefreshMicroseconds) + prediction + "; " + decksText + "\n"
            FileHandle.standardError.write(Data(line.utf8))
        }
        windowStart = frameTime
        frames = 1
        totalProcessing = 0
        maxProcessing = 0
        intervals = 0
        intervalSum = 0
        intervalSquares = 0
        totalLatency = 0
        totalEarliestLatency = 0
        maxLatency = 0
        latencySamples = 0
        totalCommit = 0
        commitSamples = 0
        repeatedFrames = 0
        refreshes = 0
        totalRefreshWork = 0
        maxRefreshWork = 0
        totalPredictionError = 0
        predictionSamples = 0
        heldFrames = [:]
        lostFrames = [:]
    }
}
