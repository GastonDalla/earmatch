import AppKit
import EarmatchCore
import os

final class OverlayController {
    var isPeeking = false {
        didSet { if isPeeking != oldValue { render() } }
    }

    var onCaptureStatusChange: (() -> Void)?
    var captureStatus: WindowCapture.Status { capture.status }

    var captureMessage: String? {
        if let problem = latest?.mappingProblem { return L10n.format("Capture does not match the rekordbox window: %@", problem.message) }
        switch capture.status {
        case .permissionDenied, .failed: return capture.status.message
        case .stopped, .starting, .waitingForWindow, .running: return nil
        }
    }

    var onLiveUpdate: ((LiveDecksSnapshot) -> Void)?

    static let unmappedFramesTolerated = 6
    static let staleFrameAge = 0.25
    static let liveUpdateInterval = 1.0 / 30

    private let capture = WindowCapture()
    private let analyzer = FrameAnalyzer()
    private let handoff = FrameHandoff()
    private let refreshDriver = DisplayLinkDriver()
    private var state = RekordboxState.initial
    private var settings = OverlaySettings()
    private var latest: FrameReading?
    private var unmappedFrames = 0
    private var master: Int?
    private var predictors: [Int: PhasePredictor] = [:]
    private var meterInputs: [PhaseMeterDeckInput] = []
    private var previousMeterInputs: [PhaseMeterDeckInput] = []
    private var previousMeterMaster: Int?
    private var previousMeterHadFrame = false
    private var meterDisplays: [Int: PhaseMeterDisplay] = [:]
    private var drawnHistory: [Int: DrawnHistory] = [:]
    private var isVisible = false
    private var wasActive = false
    private var committedFrameTime: Double?
    private var lastLiveUpdate: CFTimeInterval = 0
    private var isLiveFeedSending = false

    private var waveformPanel: OverlayPanel?
    private let waveformView = WaveformOverlayView()
    private var sideMeters: [Int: (panel: OverlayPanel, view: SideMeterView)] = [:]
    private var masks: [Int: (panel: OverlayPanel, view: BPMMaskView)] = [:]
    private var pitchCovers: [Int: OverlayPanel] = [:]
    private var headerCovers: [Int: OverlayPanel] = [:]

    init() {
        meterInputs.reserveCapacity(4)
        previousMeterInputs.reserveCapacity(4)
        capture.onFrame = { [analyzer, handoff, weak self] sample, generation in
            guard let reading = analyzer.analyze(sample, generation: generation) else { return }
            if handoff.put(reading) {
                DispatchQueue.main.async { self?.frameArrived() }
            }
        }
        capture.onStatusChange = { [weak self] status in
            guard let self else { return }
            if !status.isRunning { dropFrames() }
            render()
            onCaptureStatusChange?()
        }
        refreshDriver.onRefresh = { [weak self] timestamp, targetTime in self?.refresh(timestamp: timestamp, targetTime: targetTime) }
    }

    func update(state: RekordboxState, settings: OverlaySettings) {
        self.state = state
        self.settings = settings
        render()
    }

    func hideAll() {
        waveformPanel?.orderOut(nil)
        sideMeters.values.forEach { $0.panel.orderOut(nil) }
        masks.values.forEach { $0.panel.orderOut(nil) }
        pitchCovers.values.forEach { $0.orderOut(nil) }
        headerCovers.values.forEach { $0.orderOut(nil) }
    }

    var diagnostics: CaptureDiagnostics {
        L10n.english { englishDiagnostics }
    }

    private var englishDiagnostics: CaptureDiagnostics {
        let stats = analyzer.stats.snapshot
        let decks = (state.layout?.decks ?? []).map { deck -> CaptureDiagnostics.Deck in
            let reading = latest?.decks[deck.number]
            return CaptureDiagnostics.Deck(
                number: deck.number,
                isLoaded: Self.isLoaded(deck),
                reading: reading.map { "\($0.phase.status)" } ?? "-",
                beatSpacing: reading?.phase.beatSpacing,
                phaseInBar: reading?.phase.phaseInBar,
                beatsPerSecond: reading?.beatsPerSecond,
                isMaster: reading?.isMaster ?? false,
                headerBPM: deck.bpm,
                elapsedSeconds: deck.elapsedSeconds
            )
        }
        return CaptureDiagnostics(
            earmatchVersion: CaptureDiagnostics.earmatchVersionString,
            macOSVersion: CaptureDiagnostics.macOSVersionString,
            rekordboxVersion: AccessibilityReader.runningRekordbox()?.bundleVersion,
            accessibilityGranted: AccessibilityReader.isTrusted,
            screenRecordingGranted: CGPreflightScreenCaptureAccess(),
            activation: state.activation.message,
            layout: state.layout?.layoutName,
            capture: capture.status.message,
            mappingProblem: latest?.mappingProblem?.message,
            waveformFound: state.layout?.stackedWaveform != nil,
            framesPerSecond: stats.framesPerSecond,
            captureJitterMilliseconds: stats.captureJitterMilliseconds,
            analysisMilliseconds: stats.averageProcessingMilliseconds,
            maxAnalysisMilliseconds: stats.maxProcessingMilliseconds,
            earliestLatencyMilliseconds: stats.averageEarliestLatencyMilliseconds,
            latencyMilliseconds: stats.averageLatencyMilliseconds,
            maxLatencyMilliseconds: stats.maxLatencyMilliseconds,
            refreshesPerSecond: stats.refreshesPerSecond,
            meterErrorMilliseconds: stats.predictionErrorMilliseconds,
            decks: decks
        )
    }

    private var needsPixels: Bool {
        (settings.isBPMLimitEnabled && settings.bpmLimit != .coverFully)
            || settings.waveformMode == .split
            || settings.isPhaseMeterEnabled
            || settings.isMixTrainerEnabled
    }

    private func render() {
        syncCapture()
        let analyses = settings.isEnabled && state.activation.isActive && state.layout != nil && state.windowFrame != nil
        let visible = analyses && !isPeeking
        if visible && !isVisible {
            let cutoff = CACurrentMediaTime() - Self.staleFrameAge
            handoff.discard(capturedBefore: cutoff)
            if let latest, latest.time < cutoff { dropFrames(keepingPending: true) }
        }
        isVisible = visible
        analyzer.configure(analyses ? analyzerInput() : nil)
        guard visible, let layout = state.layout, let windowFrame = state.windowFrame else {
            refreshDriver.stop()
            hideAll()
            stopLiveFeed()
            return
        }
        refreshDriver.run(on: DisplayLinkDriver.screen(showingMostOf: windowFrame))
        placeWaveformPanel(layout: layout, windowFrame: windowFrame)
        placeMasks(layout: layout, windowFrame: windowFrame)
        placeBlindCovers(layout: layout, windowFrame: windowFrame)
        previousMeterInputs.removeAll(keepingCapacity: true)
        LayerHostView.withoutAnimation { drawFrame(layout: layout) }
    }

    private func syncCapture() {
        if settings.isEnabled && needsPixels, let pid = state.pid, let frame = state.windowFrame {
            capture.follow(pid: pid, windowFrame: frame)
            if state.activation.isActive && !wasActive { capture.windowMayBeBack() }
        } else {
            capture.follow(pid: nil, windowFrame: nil)
        }
        wasActive = state.activation.isActive
    }

    private func analyzerInput() -> FrameAnalyzer.Input? {
        guard let layout = state.layout, let windowFrame = state.windowFrame else { return nil }
        return FrameAnalyzer.Input(decks: layout.decks, windowSize: windowFrame.size, measuresBPMGlyphs: settings.isBPMLimitEnabled,
                                   readsCues: settings.isPhaseMeterEnabled && settings.counterStyle == .cueCountdown)
    }

    private func placeWaveformPanel(layout: RekordboxLayout, windowFrame: CGRect) {
        let plan = WaveformOverlayPlan(layout: layout, mode: settings.waveformMode, showsMeters: settings.isPhaseMeterEnabled,
                                       bandSize: CGSize(width: PhaseMeterLayer.compactWidth, height: PhaseMeterLayer.compactHeight))
        placeSideMeters(plan?.sideMeters ?? [:], windowFrame: windowFrame)
        guard let plan, let rect = plan.panel else {
            waveformPanel?.orderOut(nil)
            return
        }
        var strips: [Int: CGRect] = [:]
        for deck in layout.decks { strips[deck.number] = deck.waveformStrip }
        waveformView.configure(plan: plan, strips: strips)
        let panel = waveformPanel ?? OverlayPanel(contentView: waveformView)
        waveformPanel = panel
        panel.place(rect, windowFrame: windowFrame)
    }

    private func placeSideMeters(_ frames: [Int: CGRect], windowFrame: CGRect) {
        for (number, frame) in frames {
            let meter = sideMeters[number] ?? {
                let view = SideMeterView()
                return (OverlayPanel(contentView: view), view)
            }()
            sideMeters[number] = meter
            meter.panel.place(frame, windowFrame: windowFrame)
        }
        for (number, meter) in sideMeters where frames[number] == nil {
            meter.panel.orderOut(nil)
        }
    }

    private var showsMeters: Bool {
        settings.isPhaseMeterEnabled && (waveformPanel?.isVisible == true || sideMeters.values.contains { $0.panel.isVisible })
    }

    private func placeMasks(layout: RekordboxLayout, windowFrame: CGRect) {
        var shown = Set<Int>()
        if settings.isBPMLimitEnabled {
            for deck in layout.decks {
                guard let jog = deck.jogBPMFrame else { continue }
                let mask = masks[deck.number] ?? {
                    let view = BPMMaskView()
                    return (OverlayPanel(contentView: view), view)
                }()
                masks[deck.number] = mask
                mask.panel.place(jog.insetBy(dx: -BPMMaskView.margin, dy: -BPMMaskView.margin), windowFrame: windowFrame)
                shown.insert(deck.number)
            }
        }
        for (number, mask) in masks where !shown.contains(number) {
            mask.panel.orderOut(nil)
        }
    }

    private func placeBlindCovers(layout: RekordboxLayout, windowFrame: CGRect) {
        var pitches = Set<Int>(), headers = Set<Int>()
        if settings.isBlindModeEnabled {
            for deck in layout.decks {
                if let pitch = deck.pitchFrame {
                    Self.cover(&pitchCovers, deck.number, color: OverlayColor.black)
                        .place(pitch.insetBy(dx: -CoverView.margin, dy: -CoverView.margin), windowFrame: windowFrame)
                    pitches.insert(deck.number)
                }
                if let header = deck.headerBPMFrame {
                    Self.cover(&headerCovers, deck.number, color: OverlayColor.headerGrey)
                        .place(header.insetBy(dx: -CoverView.margin, dy: -CoverView.margin), windowFrame: windowFrame)
                    headers.insert(deck.number)
                }
            }
        }
        for (number, panel) in pitchCovers where !pitches.contains(number) { panel.orderOut(nil) }
        for (number, panel) in headerCovers where !headers.contains(number) { panel.orderOut(nil) }
    }

    private static func cover(_ panels: inout [Int: OverlayPanel], _ deck: Int, color: CGColor) -> OverlayPanel {
        if let panel = panels[deck] { return panel }
        let panel = OverlayPanel(contentView: CoverView(color: color))
        panels[deck] = panel
        return panel
    }

    private func frameArrived() {
        guard isVisible, let layout = state.layout, takeNewestFrame() else { return }
        LayerHostView.withoutAnimation { drawFrame(layout: layout) }
        if let latest {
            analyzer.stats.recordCommit(seconds: CACurrentMediaTime() - latest.time)
            committedFrameTime = latest.time
        }
    }

    private func refresh(timestamp: CFTimeInterval, targetTime: CFTimeInterval) {
        guard isVisible, let layout = state.layout else { return }
        let started = CACurrentMediaTime()
        let newFrame = takeNewestFrame()
        let liveDue = onLiveUpdate != nil && targetTime - lastLiveUpdate >= Self.liveUpdateInterval - 0.002
        let drawsMeters = showsMeters
        LayerHostView.withoutAnimation {
            if newFrame { drawFrame(layout: layout) }
            if drawsMeters || liveDue { computeMeters(layout: layout, at: targetTime) }
            if waveformPanel?.isVisible == true {
                for deck in layout.decks {
                    waveformView.showMeter(drawsMeters ? meterDisplays[deck.number] : nil, deck: deck.number)
                }
            }
            for (number, meter) in sideMeters where meter.panel.isVisible {
                meter.view.show(drawsMeters ? meterDisplays[number] : nil)
            }
        }
        if newFrame, let latest {
            analyzer.stats.recordCommit(seconds: CACurrentMediaTime() - latest.time)
            analyzer.stats.recordScreen(earliest: targetTime - latest.time, latest: targetTime - latest.time)
        } else if let committed = committedFrameTime {
            analyzer.stats.recordScreen(earliest: timestamp - committed, latest: targetTime - committed)
        } else {
            analyzer.stats.recordRepeatedFrame()
        }
        committedFrameTime = nil
        if liveDue {
            lastLiveUpdate = targetTime
            sendLive(layout: layout)
        }
        analyzer.stats.recordRefresh(workSeconds: CACurrentMediaTime() - started)
    }

    private func takeNewestFrame() -> Bool {
        guard let reading = handoff.take() else { return false }
        guard capture.status.isRunning, reading.generation == capture.generation else { return false }
        if FrameStats.isTracing { measureMeterError(against: reading) }
        for (number, deck) in reading.decks {
            predictors[number, default: PhasePredictor()].update(PhasePredictor.Anchor(
                time: reading.time, phaseInBar: deck.phase.phaseInBar,
                beatsPerSecond: deck.beatsPerSecond, isMeasured: deck.phase.status == .live
            ))
        }
        if reading.mappingProblem != nil, let latest, latest.mappingProblem == nil, unmappedFrames < Self.unmappedFramesTolerated {
            unmappedFrames += 1
            return false
        }
        unmappedFrames = reading.mappingProblem == nil ? 0 : unmappedFrames
        let problemChanged = reading.mappingProblem != latest?.mappingProblem
        latest = reading
        master = state.layout?.decks.first { reading.decks[$0.number]?.isMaster == true }?.number
        if problemChanged { onCaptureStatusChange?() }
        return true
    }

    private func dropFrames(keepingPending: Bool = false) {
        if !keepingPending { _ = handoff.take() }
        let problemCleared = latest?.mappingProblem != nil
        latest = nil
        master = nil
        committedFrameTime = nil
        previousMeterInputs.removeAll(keepingCapacity: true)
        unmappedFrames = 0
        predictors.removeAll(keepingCapacity: true)
        drawnHistory.removeAll(keepingCapacity: true)
        if problemCleared { onCaptureStatusChange?() }
    }

    private func drawFrame(layout: RekordboxLayout) {
        let frame = latest.flatMap { $0.mappingProblem == nil ? $0 : nil }
        if waveformPanel?.isVisible == true { waveformView.showFrame(frame, master: frame == nil ? nil : master) }
        for deck in layout.decks {
            guard let mask = masks[deck.number], mask.panel.isVisible, let jog = deck.jogBPMFrame else { continue }
            let measured = frame?.decks[deck.number]?.bpmCovers?.cover(for: settings.bpmLimit)
            let cover = measured ?? jog.insetBy(dx: -1, dy: -1)
            let origin = CGPoint(x: jog.minX - BPMMaskView.margin, y: jog.minY - BPMMaskView.margin)
            mask.view.show(cover: cover, viewOrigin: origin)
        }
    }

    private func computeMeters(layout: RekordboxLayout, at time: Double) {
        let hasFrame = latest?.mappingProblem == nil && latest != nil
        swap(&meterInputs, &previousMeterInputs)
        meterInputs.removeAll(keepingCapacity: true)
        for deck in layout.decks {
            let phase = hasFrame ? predictors[deck.number]?.phase(at: time) : nil
            if FrameStats.isTracing, let phase { drawnHistory[deck.number, default: DrawnHistory()].append(time: time, phase: phase) }
            meterInputs.append(PhaseMeterDeckInput(number: deck.number, phaseInBar: phase,
                                                   counter: counter(for: deck, phase: phase, hasFrame: hasFrame, at: time)))
        }
        guard meterInputs != previousMeterInputs || master != previousMeterMaster || hasFrame != previousMeterHadFrame else { return }
        previousMeterMaster = master
        previousMeterHadFrame = hasFrame
        for deck in layout.decks {
            meterDisplays[deck.number] = hasFrame && Self.isLoaded(deck)
                ? PhaseMeterModel.display(for: deck.number, decks: meterInputs, master: master, style: settings.phaseMeterType)
                : nil
        }
    }

    private func counter(for deck: DeckLayout, phase: Double?, hasFrame: Bool, at time: Double) -> BarPosition? {
        switch settings.counterStyle {
        case .barPosition:
            return deck.elapsedSeconds.flatMap { elapsed in
                deck.bpm.map { BarCounter.position(elapsedSeconds: elapsed, bpm: $0, phaseInBar: phase) }
            }
        case .cueCountdown:
            guard hasFrame, let latest, let measured = latest.decks[deck.number]?.cueBeatsAhead else { return nil }
            let rate = predictors[deck.number]?.beatsPerSecond ?? 0
            return CueCountdown.counter(beatsAhead: CueCountdown.beatsAhead(measured, measuredAt: latest.time, beatsPerSecond: rate, at: time))
        }
    }

    private func sendLive(layout: RekordboxLayout) {
        guard let onLiveUpdate else { return }
        let stats = analyzer.stats.snapshot
        let now = CACurrentMediaTime()
        let shown = layout.decks.count > 2 ? layout.decks.filter(Self.isLoaded) : layout.decks
        let decks = shown.map { deck in
            let predictor = predictors[deck.number]
            return LiveDeckSnapshot(number: deck.number, title: deck.title, isMaster: master == deck.number,
                                    meter: meterDisplays[deck.number],
                                    phaseInBar: predictor?.predictedPhase(at: now),
                                    beatsPerSecond: predictor?.beatsPerSecond ?? 0)
        }
        isLiveFeedSending = true
        onLiveUpdate(LiveDecksSnapshot(decks: decks, framesPerSecond: stats.framesPerSecond,
                                       latencyMilliseconds: stats.averageLatencyMilliseconds))
    }

    private func stopLiveFeed() {
        guard isLiveFeedSending else { return }
        isLiveFeedSending = false
        lastLiveUpdate = 0
        onLiveUpdate?(.empty)
    }

    private func measureMeterError(against reading: FrameReading) {
        for (number, deck) in reading.decks {
            guard deck.phase.status == .live, let measured = deck.phase.phaseInBar,
                  let rate = deck.beatsPerSecond, abs(rate) > 0.5,
                  let drawn = drawnHistory[number]?.phase(at: reading.time, beatsPerSecond: rate)
            else { continue }
            let difference = (measured - drawn + 2).truncatingRemainder(dividingBy: 4)
            let beats = (difference < 0 ? difference + 4 : difference) - 2
            analyzer.stats.recordPredictionError(seconds: beats / rate)
        }
    }

    private static func isLoaded(_ deck: DeckLayout) -> Bool {
        deck.bpm != nil || deck.title != nil
    }
}

nonisolated private struct DrawnHistory {
    private static let count = 16
    private var times = [Double](repeating: -.infinity, count: DrawnHistory.count)
    private var phases = [Double](repeating: 0, count: DrawnHistory.count)
    private var next = 0

    mutating func append(time: Double, phase: Double) {
        times[next] = time
        phases[next] = phase
        next = (next + 1) % Self.count
    }

    func phase(at time: Double, beatsPerSecond: Double) -> Double? {
        var best: Int?
        for index in times.indices where best == nil || abs(times[index] - time) < abs(times[best!] - time) {
            best = index
        }
        guard let best, abs(times[best] - time) <= 0.0084 else { return nil }
        return phases[best] + beatsPerSecond * (time - times[best])
    }
}

nonisolated private final class FrameHandoff: Sendable {
    private struct State {
        var frame: FrameReading?
        var isScheduled = false
    }

    private let state = OSAllocatedUnfairLock(uncheckedState: State())

    func put(_ newFrame: FrameReading) -> Bool {
        let (replaced, mustSchedule) = state.withLockUnchecked { state in
            let replaced = state.frame
            state.frame = newFrame
            let mustSchedule = !state.isScheduled
            state.isScheduled = true
            return (replaced, mustSchedule)
        }
        withExtendedLifetime(replaced) {}
        return mustSchedule
    }

    func take() -> FrameReading? {
        state.withLockUnchecked { state in
            state.isScheduled = false
            let taken = state.frame
            state.frame = nil
            return taken
        }
    }

    func discard(capturedBefore time: Double) {
        let stale = state.withLockUnchecked { state in
            let stale = state.frame.flatMap { $0.time < time ? $0 : nil }
            if stale != nil { state.frame = nil }
            return stale
        }
        withExtendedLifetime(stale) {}
    }
}
