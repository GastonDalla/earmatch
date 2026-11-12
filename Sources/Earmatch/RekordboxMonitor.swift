import AppKit
import EarmatchCore

struct RekordboxState: Equatable {
    var activation: OverlayActivation
    var windowFrame: CGRect?
    var layout: RekordboxLayout?
    var pid: pid_t?

    static let initial = RekordboxState(activation: .rekordboxNotRunning, windowFrame: nil, layout: nil)
}

final class RekordboxMonitor {
    var onChange: ((RekordboxState) -> Void)?
    private(set) var state = RekordboxState.initial

    private let tickInterval: TimeInterval = 0.1
    private let windowGapTolerance: TimeInterval = 0.5
    private var windowSeenAt: CFTimeInterval = 0
    private let queue = DispatchQueue(label: "earmatch.accessibility", qos: .userInteractive)
    private lazy var reader = LiveWindowReader(queue: queue) { [weak self] notification in self?.handle(notification) }
    private var timer: Timer?
    private var pollInFlight = false
    private var pollRequested = false
    private var frameReadInFlight = false
    private var frameReadRequested = false
    private var workspaceObserver: NSObjectProtocol?

    // CGPreflightScreenCaptureAccess takes several milliseconds, far too slow for a 10 Hz tick on the main
    // thread (the overlays draw there), so it is checked at most once a second on its own queue and cached here.
    private let screenRecordingCheckInterval: TimeInterval = 1
    private let permissionQueue = DispatchQueue(label: "earmatch.permissions", qos: .utility)
    private var isScreenRecordingGranted = Permissions.isScreenRecordingGranted
    private var screenRecordingCheckedAt = CACurrentMediaTime()
    private var screenRecordingCheckInFlight = false

    func start() {
        guard timer == nil else { return }
        AccessibilityReader.applyGlobalTimeout()
        let timer = Timer(timeInterval: tickInterval, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.tick() }
        }
        RunLoop.main.add(timer, forMode: .common)
        self.timer = timer
        workspaceObserver = NSWorkspace.shared.notificationCenter.addObserver(
            forName: NSWorkspace.didActivateApplicationNotification, object: nil, queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated { self?.requestPoll() }
        }
        tick()
    }

    func stop() {
        timer?.invalidate()
        timer = nil
        if let workspaceObserver { NSWorkspace.shared.notificationCenter.removeObserver(workspaceObserver) }
        workspaceObserver = nil
    }

    private func handle(_ notification: String) {
        if FrameStats.isTracing { FileHandle.standardError.write(Data("earmatch: rekordbox \(notification)\n".utf8)) }
        switch notification {
        case kAXMovedNotification, kAXWindowMovedNotification:
            readWindowFrame()
        default:
            requestPoll()
        }
    }

    private func requestPoll() {
        if pollInFlight {
            pollRequested = true
        } else {
            tick()
        }
    }

    private func tick() {
        guard !pollInFlight else { return }
        refreshScreenRecording()
        let trusted = AccessibilityReader.isTrusted
        let screenRecording = isScreenRecordingGranted
        guard trusted, screenRecording, let rekordbox = AccessibilityReader.runningRekordbox() else {
            queue.async { [reader] in reader.reset() }
            publish(RekordboxState(activation: OverlayActivation.evaluate(
                isTrusted: trusted, isScreenRecordingGranted: screenRecording, isRunning: false, layout: nil, isFrontmost: false
            )))
            return
        }
        let pid = rekordbox.processIdentifier
        pollInFlight = true
        pollRequested = false
        queue.async { [weak self, reader] in
            let poll = reader.poll(pid: pid)
            DispatchQueue.main.async {
                guard let self else { return }
                self.pollInFlight = false
                let rekordboxIsFrontmost = NSWorkspace.shared.frontmostApplication?.processIdentifier == pid
                if !poll.mainWindowIsGone,
                   self.ridesOutWindowGap(found: poll.windowFrame != nil, pid: pid, rekordboxIsFrontmost: rekordboxIsFrontmost) {
                    if self.pollRequested { self.tick() }
                    return
                }
                let activation = OverlayActivation.evaluate(
                    isTrusted: true, isScreenRecordingGranted: true, isRunning: true, layout: poll.layout,
                    isFrontmost: rekordboxIsFrontmost && poll.mainWindowIsFocused && !poll.mainWindowIsMinimized
                )
                self.publish(RekordboxState(activation: activation, windowFrame: poll.windowFrame, layout: poll.layout, pid: pid))
                if self.pollRequested { self.tick() }
            }
        }
    }

    private func refreshScreenRecording() {
        let now = CACurrentMediaTime()
        guard !screenRecordingCheckInFlight, now - screenRecordingCheckedAt >= screenRecordingCheckInterval else { return }
        screenRecordingCheckInFlight = true
        screenRecordingCheckedAt = now
        permissionQueue.async { [weak self] in
            let granted = Permissions.isScreenRecordingGranted
            DispatchQueue.main.async {
                guard let self else { return }
                self.screenRecordingCheckInFlight = false
                guard granted != self.isScreenRecordingGranted else { return }
                self.isScreenRecordingGranted = granted
                self.requestPoll()
            }
        }
    }

    private func readWindowFrame() {
        guard state.windowFrame != nil else { return }
        guard !frameReadInFlight else {
            frameReadRequested = true
            return
        }
        frameReadInFlight = true
        frameReadRequested = false
        queue.async { [weak self, reader] in
            let frame = reader.windowFrame()
            DispatchQueue.main.async {
                guard let self else { return }
                self.frameReadInFlight = false
                if let frame, self.state.windowFrame?.size == frame.size {
                    var moved = self.state
                    moved.windowFrame = frame
                    self.publish(moved)
                } else {
                    self.requestPoll()
                }
                if self.frameReadRequested { self.readWindowFrame() }
            }
        }
    }

    private func ridesOutWindowGap(found: Bool, pid: pid_t, rekordboxIsFrontmost: Bool) -> Bool {
        let now = CACurrentMediaTime()
        if found {
            windowSeenAt = now
            return false
        }
        return state.pid == pid && state.windowFrame != nil && now - windowSeenAt < windowGapTolerance
            && (rekordboxIsFrontmost || !state.activation.isActive)
    }

    private func publish(_ newState: RekordboxState) {
        guard newState != state else { return }
        state = newState
        onChange?(newState)
    }
}

nonisolated private final class LiveWindowReader: @unchecked Sendable {
    struct Poll {
        var layout: RekordboxLayout?
        var windowFrame: CGRect?
        var mainWindowIsFocused = false
        var mainWindowIsMinimized = false
        var mainWindowIsGone = false
    }

    private let queue: DispatchQueue
    private let onNotification: @MainActor (String) -> Void
    private let fullReadInterval: TimeInterval = 1.0

    private var current: AccessibilityReader.MainWindowRead?
    private var currentPID: pid_t?
    private var lastFullRead = Date.distantPast
    private var located: RekordboxLayout?
    private var headerBPMRefs: [Int: AXUIElement] = [:]
    private var timeRefs: [(index: Int, element: AXUIElement)] = []
    private var timeSnapshot: RekordboxWindowSnapshot?
    private var backoff = StallBackoff(duration: 2.0)
    private var windowIsGone = false
    private var windowListCheck = MainWindowListCheck()
    private var observedWindow: (pid: pid_t, window: AXUIElement)?
    private let observerSlot = ObserverSlot()

    init(queue: DispatchQueue, onNotification: @escaping @MainActor (String) -> Void) {
        self.queue = queue
        self.onNotification = onNotification
    }

    func poll(pid: pid_t) -> Poll {
        dispatchPrecondition(condition: .onQueue(queue))
        guard backoff.allowsPoll(at: Date()) else { return Poll() }
        do {
            let frame = try refresh(pid: pid)
            guard current != nil, var layout = located, let frame else { return Poll(mainWindowIsGone: windowIsGone) }
            var texts: [Int: String] = [:]
            for (deck, ref) in headerBPMRefs {
                if let text = try AccessibilityReader.stringValue(of: ref) { texts[deck] = text }
            }
            layout.applyLiveHeaderBPM(texts)
            try refreshTimes(into: &layout)
            return Poll(layout: layout, windowFrame: frame, mainWindowIsFocused: try AccessibilityReader.mainWindowIsFocused(pid: pid),
                        mainWindowIsMinimized: current.map { Self.isMinimized($0.window) } ?? false)
        } catch {
            reset()
            backoff.recordStall(at: Date())
            return Poll()
        }
    }

    func windowFrame() -> CGRect? {
        dispatchPrecondition(condition: .onQueue(queue))
        guard backoff.allowsPoll(at: Date()), let window = current?.window else { return nil }
        return try? AccessibilityReader.frame(of: window)
    }

    func reset() {
        dispatchPrecondition(condition: .onQueue(queue))
        current = nil
        currentPID = nil
        located = nil
        headerBPMRefs = [:]
        timeRefs = []
        timeSnapshot = nil
        lastFullRead = .distantPast
    }

    private func refresh(pid: pid_t) throws -> CGRect? {
        var frame = try current.flatMap { try AccessibilityReader.frame(of: $0.window) }
        let needsFullRead = pid != currentPID || current == nil || frame == nil
            || frame?.size != current?.snapshot.windowFrame.size
            || Date().timeIntervalSince(lastFullRead) >= fullReadInterval
        guard needsFullRead else { return frame }

        if pid != currentPID { windowListCheck = MainWindowListCheck() }
        reset()
        currentPID = pid
        lastFullRead = Date()
        guard let read = try AccessibilityReader.readMainWindow(pid: pid) else {
            windowIsGone = windowListCheck.isMainWindowGone(windowTitles: Self.windowTitles(pid: pid))
            return nil
        }
        windowIsGone = false
        windowListCheck.mainWindowFound()
        current = read
        frame = read.snapshot.windowFrame
        let layout = RekordboxLayout.locate(in: read.snapshot)
        located = layout
        for (deck, index) in layout.headerBPMElementIndices(in: read.snapshot) {
            headerBPMRefs[deck] = read.elementRefs[index]
        }
        timeSnapshot = read.snapshot
        timeRefs = Self.timeElementIndices(in: read.snapshot, layout: layout).map { ($0, read.elementRefs[$0]) }
        observe(pid: pid, window: read.window)
        return frame
    }

    private static func windowTitles(pid: pid_t) -> [String]? {
        let app = AXUIElementCreateApplication(pid)
        var value: AnyObject?
        guard AXUIElementCopyAttributeValue(app, kAXWindowsAttribute as CFString, &value) == .success,
              let windows = value as? [AXUIElement]
        else { return nil }
        var titles: [String] = []
        for window in windows {
            var title: AnyObject?
            guard AXUIElementCopyAttributeValue(window, kAXTitleAttribute as CFString, &title) == .success else { return nil }
            titles.append(title as? String ?? "")
        }
        return titles
    }

    private static func isMinimized(_ window: AXUIElement) -> Bool {
        var value: AnyObject?
        guard AXUIElementCopyAttributeValue(window, kAXMinimizedAttribute as CFString, &value) == .success else { return false }
        return (value as? Bool) == true
    }

    private func refreshTimes(into layout: inout RekordboxLayout) throws {
        guard var snapshot = timeSnapshot, !timeRefs.isEmpty else { return }
        for (index, element) in timeRefs {
            guard let text = try AccessibilityReader.stringValue(of: element) else { continue }
            snapshot.elements[index].value = text.isEmpty ? nil : text
        }
        timeSnapshot = snapshot
        let relocated = RekordboxLayout.locate(in: snapshot)
        for index in layout.decks.indices {
            guard let deck = relocated.decks.first(where: { $0.number == layout.decks[index].number }) else { continue }
            layout.decks[index].elapsedSeconds = deck.elapsedSeconds
            layout.decks[index].remainingSeconds = deck.remainingSeconds
        }
    }

    private static func timeElementIndices(in snapshot: RekordboxWindowSnapshot, layout: RekordboxLayout) -> [Int] {
        let rows = layout.decks.map(\.masterButtonFrame.minY)
        return snapshot.elements.indices.filter { index in
            let element = snapshot.elements[index]
            guard element.role == "AXStaticText", element.frame.height <= 18,
                  rows.contains(where: { abs($0 - element.frame.minY) <= 6 }),
                  let value = element.value
            else { return false }
            return value.range(of: #"^(-?\d{2}:\d{2}|\.\d)$"#, options: .regularExpression) != nil
        }
    }

    private func observe(pid: pid_t, window: AXUIElement) {
        if let observedWindow, observedWindow.pid == pid, CFEqual(observedWindow.window, window) { return }
        guard let observer = WindowNotificationObserver(pid: pid, window: window, handler: onNotification) else { return }
        observedWindow = (pid, window)
        DispatchQueue.main.async { [observerSlot] in observerSlot.replace(with: observer) }
    }
}

private final class ObserverSlot {
    private var observer: WindowNotificationObserver?

    nonisolated init() {}

    func replace(with newObserver: WindowNotificationObserver?) {
        observer = newObserver
    }
}

nonisolated private final class WindowNotificationObserver: @unchecked Sendable {
    private let observer: AXObserver
    private let handler: @MainActor (String) -> Void

    private static let appNotifications = [
        kAXFocusedWindowChangedNotification, kAXMainWindowChangedNotification,
        kAXWindowMovedNotification, kAXWindowResizedNotification,
    ]
    private static let windowNotifications = [kAXMovedNotification, kAXResizedNotification]

    init?(pid: pid_t, window: AXUIElement, handler: @escaping @MainActor (String) -> Void) {
        self.handler = handler
        var created: AXObserver?
        let callback: AXObserverCallback = { _, _, notification, refcon in
            guard let refcon else { return }
            let handler = Unmanaged<WindowNotificationObserver>.fromOpaque(refcon).takeUnretainedValue().handler
            let name = notification as String
            MainActor.assumeIsolated { handler(name) }
        }
        guard AXObserverCreate(pid, callback, &created) == .success, let created else { return nil }
        observer = created
        let refcon = Unmanaged.passUnretained(self).toOpaque()
        let app = AXUIElementCreateApplication(pid)
        var registered = 0
        for name in Self.appNotifications where AXObserverAddNotification(created, app, name as CFString, refcon) == .success { registered += 1 }
        for name in Self.windowNotifications where AXObserverAddNotification(created, window, name as CFString, refcon) == .success { registered += 1 }
        if FrameStats.isTracing {
            let total = Self.appNotifications.count + Self.windowNotifications.count
            FileHandle.standardError.write(Data("earmatch: observing \(registered) of \(total) rekordbox window notifications\n".utf8))
        }
        CFRunLoopAddSource(CFRunLoopGetMain(), AXObserverGetRunLoopSource(created), .commonModes)
    }

    deinit {
        CFRunLoopRemoveSource(CFRunLoopGetMain(), AXObserverGetRunLoopSource(observer), .commonModes)
    }
}
