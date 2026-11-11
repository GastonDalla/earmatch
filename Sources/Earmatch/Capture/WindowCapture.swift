import AppKit
import CoreMedia
import EarmatchCore
import QuartzCore
import ScreenCaptureKit

final class WindowCapture: NSObject, SCStreamOutput, SCStreamDelegate {
    enum Status: Equatable {
        case stopped
        case permissionDenied
        case starting
        case waitingForWindow
        case running
        case failed(String)

        var isRunning: Bool { self == .running }

        var message: String {
            switch self {
            case .stopped: L10n.text("Capture stopped")
            case .permissionDenied: L10n.text("Screen Recording is not allowed for Earmatch")
            case .starting: L10n.text("Starting capture of the rekordbox window")
            case .waitingForWindow: L10n.text("Waiting for the rekordbox window to be on screen")
            case .running: L10n.text("Capturing the rekordbox window")
            case let .failed(reason): L10n.format("Capture failed: %@", reason)
            }
        }
    }

    var onFrame: ((CMSampleBuffer, Int) -> Void)?
    var onStatusChange: ((Status) -> Void)?

    private(set) var status: Status = .stopped {
        didSet { if status != oldValue { onStatusChange?(status) } }
    }

    let queue = DispatchQueue(label: "earmatch.capture", qos: .userInteractive)

    private struct Target: Equatable {
        let pid: pid_t
        let windowFrame: CGRect
    }

    private var wanted: Target?
    private var stream: SCStream?
    private var configuration: SCStreamConfiguration?
    private var streamedSize: CGSize?
    private(set) var generation = 0
    private let deliveryLock = NSLock()
    private var delivering: (stream: ObjectIdentifier, generation: Int)?
    private var schedule = RetrySchedule()
    private static var askedForPermission = false

    func follow(pid: pid_t?, windowFrame: CGRect?) {
        guard let pid, let windowFrame else {
            stop()
            return
        }
        let target = Target(pid: pid, windowFrame: windowFrame)
        let samePID = wanted?.pid == pid
        wanted = target
        switch status {
        case .running where samePID:
            if streamedSize != windowFrame.size { resize(to: windowFrame.size) }
        case .starting where samePID, .waitingForWindow where samePID:
            break
        case .failed, .permissionDenied:
            if schedule.allowsAttempt(at: CACurrentMediaTime()) { start() }
        default:
            start()
        }
    }

    func windowMayBeBack() {
        guard status == .waitingForWindow, wanted != nil else { return }
        schedule.reset()
        start()
    }

    func stop() {
        wanted = nil
        schedule.reset()
        generation += 1
        setDelivering(nil)
        stream?.stopCapture(completionHandler: nil)
        stream = nil
        configuration = nil
        streamedSize = nil
        status = .stopped
    }

    private func start() {
        guard let target = wanted else { return }
        schedule.attemptStarted()
        guard CGPreflightScreenCaptureAccess() else {
            if !Self.askedForPermission {
                Self.askedForPermission = true
                CGRequestScreenCaptureAccess()
            }
            retry(.permissionDenied, as: .permissionDenied)
            return
        }
        generation += 1
        let startGeneration = generation
        setDelivering(nil)
        stream?.stopCapture(completionHandler: nil)
        stream = nil
        if status != .waitingForWindow { status = .starting }
        SCShareableContent.getExcludingDesktopWindows(false, onScreenWindowsOnly: false) { [weak self] content, error in
            DispatchQueue.main.async {
                guard let self, self.generation == startGeneration else { return }
                guard let content else {
                    self.fail(error?.localizedDescription ?? L10n.text("no shareable content"))
                    return
                }
                guard let window = Self.mainWindow(in: content, target: target) else {
                    self.fail(L10n.text("rekordbox window not found"))
                    return
                }
                guard window.isOnScreen else {
                    self.waitForWindow()
                    return
                }
                self.schedule.windowFound()
                self.startStream(on: window, target: target, generation: startGeneration)
            }
        }
    }

    private static func mainWindow(in content: SCShareableContent, target: Target) -> SCWindow? {
        let candidates = content.windows
            .filter { $0.owningApplication?.processID == target.pid && $0.windowLayer == 0 && $0.title == "rekordbox" }
        let onScreen = candidates.filter(\.isOnScreen)
        return (onScreen.isEmpty ? candidates : onScreen)
            .min { distance($0.frame, target.windowFrame) < distance($1.frame, target.windowFrame) }
    }

    private func waitForWindow() {
        retry(.windowOffScreen, as: .waitingForWindow)
    }

    private static func distance(_ a: CGRect, _ b: CGRect) -> CGFloat {
        abs(a.minX - b.minX) + abs(a.minY - b.minY) + abs(a.width - b.width) + abs(a.height - b.height)
    }

    private func startStream(on window: SCWindow, target: Target, generation startGeneration: Int) {
        let filter = SCContentFilter(desktopIndependentWindow: window)
        let configuration = SCStreamConfiguration()
        let scale = CGFloat(filter.pointPixelScale)
        configuration.width = Int((window.frame.width * scale).rounded())
        configuration.height = Int((window.frame.height * scale).rounded())
        configuration.pixelFormat = kCVPixelFormatType_32BGRA
        configuration.showsCursor = false
        configuration.ignoreShadowsSingleWindow = true
        configuration.captureResolution = .best
        configuration.minimumFrameInterval = CMTime(value: 1, timescale: CMTimeScale(Self.refreshRate(for: target.windowFrame)))
        configuration.queueDepth = 4

        let stream = SCStream(filter: filter, configuration: configuration, delegate: self)
        do {
            try stream.addStreamOutput(self, type: .screen, sampleHandlerQueue: queue)
        } catch {
            fail(error.localizedDescription)
            return
        }
        self.stream = stream
        self.configuration = configuration
        streamedSize = window.frame.size
        setDelivering((ObjectIdentifier(stream), startGeneration))
        stream.startCapture { [weak self] error in
            DispatchQueue.main.async {
                guard let self, self.generation == startGeneration else { return }
                if let error {
                    self.fail(error.localizedDescription)
                } else {
                    self.status = .running
                    if let size = self.wanted?.windowFrame.size, size != self.streamedSize { self.resize(to: size) }
                }
            }
        }
    }

    private static func refreshRate(for quartzFrame: CGRect) -> Int {
        let screen = DisplayLinkDriver.screen(showingMostOf: quartzFrame) ?? NSScreen.main
        return min(max(screen?.maximumFramesPerSecond ?? 60, 30), 120)
    }

    private func resize(to size: CGSize) {
        guard let stream, let configuration else { return }
        let scale = CGFloat(configuration.width) / max(streamedSize?.width ?? size.width, 1)
        configuration.width = Int((size.width * scale).rounded())
        configuration.height = Int((size.height * scale).rounded())
        streamedSize = size
        stream.updateConfiguration(configuration, completionHandler: nil)
    }

    private func fail(_ reason: String) {
        setDelivering(nil)
        stream = nil
        configuration = nil
        streamedSize = nil
        retry(.failure, as: .failed(reason))
    }

    private func retry(_ reason: RetrySchedule.Reason, as newStatus: Status) {
        if let timer = schedule.retry(reason, at: CACurrentMediaTime()) {
            DispatchQueue.main.asyncAfter(deadline: .now() + timer.delay) { [weak self] in
                guard let self, schedule.fire(timer) else { return }
                guard wanted != nil, !status.isRunning, status != .starting else { return }
                start()
            }
        }
        status = newStatus
    }

    func stream(_ stream: SCStream, didOutputSampleBuffer sampleBuffer: CMSampleBuffer, of type: SCStreamOutputType) {
        guard type == .screen, Self.frameStatus(of: sampleBuffer) == .complete else { return }
        deliveryLock.lock()
        let current = delivering
        deliveryLock.unlock()
        guard let current, current.stream == ObjectIdentifier(stream) else { return }
        onFrame?(sampleBuffer, current.generation)
    }

    private func setDelivering(_ value: (stream: ObjectIdentifier, generation: Int)?) {
        deliveryLock.lock()
        delivering = value
        deliveryLock.unlock()
    }

    func stream(_ stream: SCStream, didStopWithError error: Error) {
        DispatchQueue.main.async { [weak self] in
            guard let self, stream === self.stream else { return }
            fail(error.localizedDescription)
        }
    }

    static func frameStatus(of sampleBuffer: CMSampleBuffer) -> SCFrameStatus? {
        guard let attachments = CMSampleBufferGetSampleAttachmentsArray(sampleBuffer, createIfNecessary: false) as? [[SCStreamFrameInfo: Any]],
              let raw = attachments.first?[.status] as? Int
        else { return nil }
        return SCFrameStatus(rawValue: raw)
    }
}
