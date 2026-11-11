import AppKit
import QuartzCore
import EarmatchCore

final class AppDelegate: NSObject, NSApplicationDelegate {
    private let store = SettingsStore()
    private let monitor = RekordboxMonitor()
    private let overlay = OverlayController()
    private let hotkeys = HotkeyMonitor()
    private let practice = PracticeController()
    private var menuBar: MenuBarController?
    private var mainWindow: MainWindowController?
    private var menuBarIconIsActive: Bool?

    func applicationDidFinishLaunching(_ notification: Notification) {
        AppUpdater.shared.start()
        let menuBar = MenuBarController(store: store, monitor: monitor)
        let mainWindow = MainWindowController(store: store, practice: practice)
        self.menuBar = menuBar
        self.mainWindow = mainWindow

        menuBar.onOpenMainWindow = { [weak mainWindow] in mainWindow?.show() }
        menuBar.onCopyDiagnostics = { [weak mainWindow] in mainWindow?.copyDiagnostics() }
        mainWindow.extraDiagnostics = { [weak overlay] in overlay?.diagnostics.text ?? "" }
        mainWindow.onLanguageChange = { [weak self, weak mainWindow] in
            guard let self, let mainWindow else { return }
            mainWindow.updateCapture(problem: overlay.captureMessage)
            mainWindow.update(state: monitor.state)
        }

        hotkeys.onPeekChanged = { [weak self] peeking in
            guard let self else { return }
            overlay.isPeeking = peeking
            render(monitor.state)
        }
        hotkeys.onToggleEnabled = { [weak self] in self?.store.update { $0.isEnabled.toggle() } }
        hotkeys.start()

        overlay.onCaptureStatusChange = { [weak self, weak mainWindow] in
            guard let self, let mainWindow else { return }
            mainWindow.updateCapture(problem: overlay.captureMessage)
            mainWindow.update(state: monitor.state)
        }
        overlay.onLiveUpdate = { [weak self, weak mainWindow] snapshot in
            mainWindow?.updateLive(snapshot)
            self?.practice.ingest(snapshot, at: CACurrentMediaTime())
        }
        monitor.onChange = { [weak self] state in self?.render(state) }
        NotificationCenter.default.addObserver(forName: SettingsStore.didChange, object: store, queue: .main) { [weak self] _ in
            MainActor.assumeIsolated {
                guard let self else { return }
                self.render(self.monitor.state)
            }
        }

        if !AccessibilityReader.isTrusted { AccessibilityReader.requestTrust() }
        monitor.start()
        mainWindow.update(state: monitor.state)
        mainWindow.show()
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        mainWindow?.show()
        return false
    }

    private func render(_ state: RekordboxState) {
        overlay.update(state: state, settings: store.settings)
        let isActive = state.activation.isActive && store.settings.isEnabled
        if isActive != menuBarIconIsActive {
            menuBarIconIsActive = isActive
            menuBar?.refreshIcon(isActive: isActive)
        }
        mainWindow?.update(state: state)
    }
}
