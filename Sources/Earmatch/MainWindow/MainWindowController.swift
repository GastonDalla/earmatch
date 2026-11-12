import AppKit
import EarmatchCore
import QuartzCore
import SwiftUI

final class MainWindowController: NSObject, NSWindowDelegate {
    var extraDiagnostics: () -> String = { "" }
    var onLanguageChange: () -> Void = {}

    private static let liveSnapshotLifetime: TimeInterval = 0.75
    private static let frameAutosaveName = "EarmatchSidebarWindow"

    private let store: SettingsStore
    private let model: MainWindowModel
    private let live = LiveDecksModel()
    private let practice: PracticeController
    private var window: NSWindow?
    private var state = RekordboxState.initial
    private var permissionTimer: Timer?
    private var liveExpiryTimer: Timer?
    private var liveReceivedAt: CFTimeInterval = 0
    private var activationObserver: NSObjectProtocol?
    private var inputMonitor: Any?
    private var cachedVersion: (pid: pid_t, version: String?)?

    init(store: SettingsStore, practice: PracticeController) {
        self.store = store
        self.practice = practice
        model = MainWindowModel(store: store,
                                rememberedPane: UserDefaults.standard.string(forKey: MainWindowPane.storageKey),
                                language: AppLanguage.choice,
                                canCheckForUpdates: AppUpdater.shared.isAvailable)
        super.init()
        model.onCopyDiagnostics = { [weak self] in self?.copyDiagnostics() }
        model.onCheckForUpdates = { AppUpdater.shared.checkForUpdates() }
        model.onPaneSelected = { pane in UserDefaults.standard.set(pane.rawValue, forKey: MainWindowPane.storageKey) }
        model.onLanguageSelected = { [weak self] choice in self?.changeLanguage(to: choice) }
        activationObserver = NotificationCenter.default.addObserver(
            forName: NSApplication.didBecomeActiveNotification, object: nil, queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated { self?.model.refreshPermissions() }
        }
    }

    isolated deinit {
        permissionTimer?.invalidate()
        liveExpiryTimer?.invalidate()
        if let activationObserver { NotificationCenter.default.removeObserver(activationObserver) }
        if let inputMonitor { NSEvent.removeMonitor(inputMonitor) }
    }

    func show() {
        let window = window ?? makeWindow()
        self.window = window
        if !window.isVisible {
            // The model skips monitor updates while the window is hidden; catch up before deciding whether it opens locked.
            applyState()
            model.windowDidOpen()
        }
        startPermissionChecks()
        installMenus(replacing: false)
        if NSApp.activationPolicy() != .regular { NSApp.setActivationPolicy(.regular) }
        NSApp.activate()
        window.makeKeyAndOrderFront(nil)
        applyState()
        updateVisibility()
    }

    func update(state: RekordboxState) {
        self.state = state
        if !state.activation.isActive { clearLive() }
        if model.isVisible { applyState() }
    }

    func updateCapture(problem: String?) {
        model.apply(captureProblem: problem)
    }

    func updateLive(_ snapshot: LiveDecksSnapshot) {
        guard model.isVisible else { return }
        liveReceivedAt = CACurrentMediaTime()
        live.apply(snapshot, at: liveReceivedAt)
        if liveExpiryTimer == nil { armLiveExpiry(after: Self.liveSnapshotLifetime) }
    }

    func diagnosticsText() -> String {
        DiagnosticsReport(
            appVersion: SystemInfo.appVersion,
            macOS: SystemInfo.macOSVersion,
            hardware: SystemInfo.hardwareModel,
            architecture: SystemInfo.architecture,
            rekordboxVersion: rekordboxVersion(pid: state.pid ?? AccessibilityReader.runningRekordbox()?.processIdentifier),
            isAccessibilityGranted: AccessibilityReader.isTrusted,
            isScreenRecordingGranted: Permissions.isScreenRecordingGranted,
            activation: state.activation,
            settings: store.settings,
            extra: extraDiagnostics()
        ).text
    }

    func select(_ pane: MainWindowPane) {
        model.select(pane)
    }

    private func changeLanguage(to choice: AppLanguageChoice) {
        AppLanguage.select(choice)
        installMenus(replacing: true)
        onLanguageChange()
    }

    private func installMenus(replacing: Bool) {
        MainMenu.install(replacing: replacing, selectPane: { [weak self] pane in self?.select(pane) })
    }

    func copyDiagnostics() {
        let pasteboard = NSPasteboard.general
        pasteboard.clearContents()
        pasteboard.setString(diagnosticsText(), forType: .string)
    }

    func windowWillClose(_ notification: Notification) {
        permissionTimer?.invalidate()
        permissionTimer = nil
        model.isVisible = false
        clearLive()
        NSApp.setActivationPolicy(.accessory)
    }

    func windowDidChangeOcclusionState(_ notification: Notification) {
        updateVisibility()
    }

    func windowDidMiniaturize(_ notification: Notification) {
        updateVisibility()
    }

    func windowDidDeminiaturize(_ notification: Notification) {
        updateVisibility()
    }

    private func startPermissionChecks() {
        guard permissionTimer == nil else { return }
        let timer = Timer(timeInterval: 1, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.model.refreshPermissions() }
        }
        RunLoop.main.add(timer, forMode: .common)
        permissionTimer = timer
    }

    private func updateVisibility() {
        guard let window else { return }
        let visible = window.isVisible && !window.isMiniaturized && window.occlusionState.contains(.visible)
        if visible != model.isVisible {
            model.isVisible = visible
            if visible { applyState() }
        }
        if !visible { clearLive() }
    }

    private func applyState() {
        model.apply(activation: state.activation, rekordboxVersion: rekordboxVersion(pid: state.pid), layoutName: state.layout?.layoutName)
    }

    private func armLiveExpiry(after delay: TimeInterval) {
        let timer = Timer(timeInterval: delay, repeats: false) { [weak self] _ in
            MainActor.assumeIsolated { self?.liveExpiryFired() }
        }
        RunLoop.main.add(timer, forMode: .common)
        liveExpiryTimer = timer
    }

    private func liveExpiryFired() {
        liveExpiryTimer = nil
        let remaining = liveReceivedAt + Self.liveSnapshotLifetime - CACurrentMediaTime()
        if remaining > 0.001 {
            armLiveExpiry(after: remaining)
        } else {
            live.clear()
        }
    }

    private func clearLive() {
        liveExpiryTimer?.invalidate()
        liveExpiryTimer = nil
        live.clear()
    }

    private func makeWindow() -> NSWindow {
        let size = Self.fittedSize(MainWindowMetrics.defaultSize, minimum: MainWindowMetrics.minimumSize)
        let window = MainWindow(
            contentRect: NSRect(origin: .zero, size: size),
            styleMask: [.titled, .closable, .miniaturizable, .resizable, .fullSizeContentView],
            backing: .buffered,
            defer: false
        )
        window.title = "Earmatch"
        window.titlebarAppearsTransparent = true
        window.appearance = NSAppearance(named: .darkAqua)
        window.backgroundColor = NSColor(WindowTheme.background)
        window.isReleasedWhenClosed = false
        window.tabbingMode = .disallowed
        window.contentMinSize = MainWindowMetrics.minimumSize
        window.delegate = self

        let hosting = NSHostingView(rootView: MainWindowView(model: model, live: live, practice: practice))
        hosting.sizingOptions = []
        window.contentView = hosting
        window.setContentSize(size)
        if !window.setFrameUsingName(Self.frameAutosaveName) { window.center() }
        window.setFrameAutosaveName(Self.frameAutosaveName)
        watchInput(in: window)
        return window
    }

    private static func fittedSize(_ size: CGSize, minimum: CGSize) -> NSSize {
        guard let visible = NSScreen.main?.visibleFrame.size else { return size }
        return NSSize(width: max(minimum.width, min(size.width, visible.width)),
                      height: max(minimum.height, min(size.height, visible.height)))
    }

    private func watchInput(in window: NSWindow) {
        inputMonitor = NSEvent.addLocalMonitorForEvents(matching: [.keyDown, .leftMouseDown]) { [weak self, weak window] event in
            guard let self, let window, event.window === window else { return event }
            let isKeyboard = event.type == .keyDown
            if model.isKeyboardNavigating != isKeyboard { model.isKeyboardNavigating = isKeyboard }
            return event
        }
    }

    private func rekordboxVersion(pid: pid_t?) -> String? {
        guard let pid else { return nil }
        if let cachedVersion, cachedVersion.pid == pid { return cachedVersion.version }
        guard let app = NSRunningApplication(processIdentifier: pid) else { return nil }
        let version = app.bundleURL.flatMap(Bundle.init(url:))?.infoDictionary?["CFBundleShortVersionString"] as? String
        cachedVersion = (pid, version)
        return version
    }
}

private final class MainWindow: NSWindow {
    override func performKeyEquivalent(with event: NSEvent) -> Bool {
        let flags = event.modifierFlags.intersection(.deviceIndependentFlagsMask)
        if flags == .command, event.charactersIgnoringModifiers == "w" {
            performClose(nil)
            return true
        }
        return super.performKeyEquivalent(with: event)
    }
}
