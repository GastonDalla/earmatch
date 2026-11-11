import AppKit
import EarmatchCore
import Sparkle

final class AppUpdater: NSObject, SPUStandardUserDriverDelegate {
    static let shared = AppUpdater()

    static let feedURLKey = "SUFeedURL"
    static let publicKeyKey = "SUPublicEDKey"

    static func isConfigured(_ info: [String: Any]) -> Bool {
        [feedURLKey, publicKeyKey].allSatisfy { key in
            guard let value = info[key] as? String else { return false }
            return !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        }
    }

    private var controller: SPUStandardUpdaterController?
    private var startedAt = ProcessInfo.processInfo.systemUptime
    private let menuItems = NSHashTable<NSMenuItem>.weakObjects()

    private var hasPendingReminder = false {
        didSet { menuItems.allObjects.forEach { $0.title = menuTitle } }
    }

    var isAvailable: Bool { controller != nil }

    func start(info: [String: Any] = Bundle.main.infoDictionary ?? [:]) {
        guard controller == nil, Self.isConfigured(info) else { return }
        let controller = SPUStandardUpdaterController(startingUpdater: true, updaterDelegate: nil, userDriverDelegate: self)
        self.controller = controller
        startedAt = ProcessInfo.processInfo.systemUptime
        if controller.updater.automaticallyChecksForUpdates {
            controller.updater.checkForUpdatesInBackground()
        }
    }

    func checkForUpdates() {
        guard let controller else { return }
        NSApp.activate()
        controller.updater.checkForUpdates()
    }

    func makeMenuItem() -> NSMenuItem? {
        guard isAvailable else { return nil }
        let item = ClosureMenuItem(title: menuTitle) { [weak self] in self?.checkForUpdates() }
        menuItems.add(item)
        return item
    }

    private var menuTitle: String {
        hasPendingReminder ? L10n.text("Update Available…") : L10n.text("Check for Updates…")
    }

    var supportsGentleScheduledUpdateReminders: Bool { true }

    func standardUserDriverShouldHandleShowingScheduledUpdate(_ update: SUAppcastItem, andInImmediateFocus immediateFocus: Bool) -> Bool {
        false
    }

    func standardUserDriverWillHandleShowingUpdate(_ handleShowingUpdate: Bool, forUpdate update: SUAppcastItem, state: SPUUserUpdateState) {
        guard !handleShowingUpdate else { return }
        hasPendingReminder = true
        let asks = UpdatePrompt.asksNow(
            secondsSinceLaunch: ProcessInfo.processInfo.systemUptime - startedAt,
            rekordboxIsFrontmost: NSWorkspace.shared.frontmostApplication?.bundleIdentifier == AccessibilityReader.rekordboxBundleID
        )
        guard asks else { return }
        Task { self.checkForUpdates() }
    }

    func standardUserDriverDidReceiveUserAttention(forUpdate update: SUAppcastItem) {
        hasPendingReminder = false
    }

    func standardUserDriverWillFinishUpdateSession() {
        hasPendingReminder = false
    }
}
