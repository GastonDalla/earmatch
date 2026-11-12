import AppKit
import Observation
import SwiftUI
import EarmatchCore

@Observable
final class MainWindowModel {
    private(set) var settings: OverlaySettings
    private(set) var activation: OverlayActivation = RekordboxState.initial.activation
    private(set) var rekordboxVersion: String?
    private(set) var layoutName: String?
    private(set) var isAccessibilityGranted: Bool
    private(set) var isScreenRecordingGranted: Bool
    /// The window shows only its permissions screen while this is on (see `PermissionLock`).
    private(set) var isLocked: Bool
    private(set) var pane: MainWindowPane
    private(set) var language: AppLanguageChoice
    var isVisible = false
    private(set) var captureProblem: String?
    var isKeyboardNavigating = false

    var versionLabel: String { SystemInfo.shortVersion.map { "v\($0)" } ?? L10n.text("development build") }
    let canCheckForUpdates: Bool
    @ObservationIgnored var onCopyDiagnostics: () -> Void = {}
    @ObservationIgnored var onCheckForUpdates: () -> Void = {}
    @ObservationIgnored var onPaneSelected: (MainWindowPane) -> Void = { _ in }
    @ObservationIgnored var onLanguageSelected: (AppLanguageChoice) -> Void = { _ in }

    @ObservationIgnored private let store: SettingsStore?
    @ObservationIgnored private let readPermissions: () -> (accessibility: Bool, screenRecording: Bool)
    @ObservationIgnored private var settingsObserver: NSObjectProtocol?
    @ObservationIgnored private var lock: PermissionLock
    @ObservationIgnored private var unlockTask: Task<Void, Never>?

    init(store: SettingsStore, rememberedPane: String?, language: AppLanguageChoice, canCheckForUpdates: Bool) {
        let permissions = Self.systemPermissions()
        let arePermissionsMissing = !permissions.accessibility || !permissions.screenRecording
        lock = PermissionLock(arePermissionsMissing: arePermissionsMissing, activation: RekordboxState.initial.activation)
        isLocked = lock.isLocked
        self.store = store
        self.language = language
        self.canCheckForUpdates = canCheckForUpdates
        readPermissions = Self.systemPermissions
        settings = store.settings
        isAccessibilityGranted = permissions.accessibility
        isScreenRecordingGranted = permissions.screenRecording
        pane = MainWindowPane.initial(stored: rememberedPane, arePermissionsMissing: arePermissionsMissing)
        settingsObserver = NotificationCenter.default.addObserver(forName: SettingsStore.didChange, object: store, queue: .main) { [weak self] _ in
            MainActor.assumeIsolated {
                guard let self, let store = self.store else { return }
                let latest = store.settings
                if latest != self.settings { self.settings = latest }
            }
        }
    }

    init(sample settings: OverlaySettings, pane: MainWindowPane, context: StatusContext) {
        store = nil
        canCheckForUpdates = true
        readPermissions = { (true, true) }
        self.settings = settings
        self.pane = pane
        language = .system
        isAccessibilityGranted = true
        isScreenRecordingGranted = context.isScreenRecordingGranted
        activation = context.activation
        rekordboxVersion = context.rekordboxVersion
        layoutName = context.layoutName
        lock = PermissionLock(arePermissionsMissing: !context.isScreenRecordingGranted, activation: context.activation)
        isLocked = lock.isLocked
        isVisible = true
    }

    isolated deinit {
        unlockTask?.cancel()
        if let settingsObserver { NotificationCenter.default.removeObserver(settingsObserver) }
    }

    var status: StatusContext {
        StatusContext(activation: activation, settings: settings, isScreenRecordingGranted: isScreenRecordingGranted,
                      rekordboxVersion: rekordboxVersion, captureProblem: captureProblem, layoutName: layoutName)
    }

    var arePermissionsMissing: Bool { !isAccessibilityGranted || !isScreenRecordingGranted }

    /// True until both the window and the monitor see both permissions; the lock lifts a moment after it turns false.
    var isWaitingForPermissions: Bool {
        PermissionLock.isWaiting(arePermissionsMissing: arePermissionsMissing, activation: activation)
    }

    var windowStatus: MainWindowStatus {
        MainWindowStatus(arePermissionsMissing: arePermissionsMissing, isEnabled: settings.isEnabled, activation: activation)
    }

    func windowDidOpen() {
        refreshPermissions()
        // A window that opens with both permissions allowed shows its options at once, without the unlock delay.
        cancelUnlock()
        lock = PermissionLock(arePermissionsMissing: arePermissionsMissing, activation: activation)
        if lock.isLocked != isLocked { isLocked = lock.isLocked }
        if arePermissionsMissing { pane = .setup }
    }

    func select(_ newPane: MainWindowPane) {
        // The View menu's Command-1 to Command-4 stay inert while only the permissions screen is shown.
        guard !isLocked, newPane != pane else { return }
        pane = newPane
        onPaneSelected(newPane)
    }

    var languageBinding: Binding<AppLanguageChoice> {
        Binding(
            get: { [weak self] in self?.language ?? .system },
            set: { [weak self] choice in self?.selectLanguage(choice) }
        )
    }

    func selectLanguage(_ choice: AppLanguageChoice) {
        guard choice != language else { return }
        onLanguageSelected(choice)
        language = choice
    }

    func binding<Value>(_ keyPath: WritableKeyPath<OverlaySettings, Value>) -> Binding<Value> {
        Binding(
            get: { [weak self] in self?.settings[keyPath: keyPath] ?? OverlaySettings()[keyPath: keyPath] },
            set: { [weak self] value in self?.store?.update { $0[keyPath: keyPath] = value } }
        )
    }

    var boothPreset: Binding<BoothPreset?> {
        Binding(
            get: { [weak self] in self.flatMap { BoothPreset.selected(in: $0.settings) } },
            set: { [weak self] preset in
                guard let preset else { return }
                self?.store?.update { preset.apply(to: &$0) }
            }
        )
    }

    func apply(activation newActivation: OverlayActivation, rekordboxVersion newVersion: String?, layoutName newLayout: String?) {
        if newActivation != activation { activation = newActivation }
        if newVersion != rekordboxVersion { rekordboxVersion = newVersion }
        if newLayout != layoutName { layoutName = newLayout }
        if isVisible { refreshPermissions() } else { updateLock() }
    }

    func apply(captureProblem newProblem: String?) {
        if newProblem != captureProblem { captureProblem = newProblem }
    }

    func refreshPermissions() {
        let permissions = readPermissions()
        if permissions.accessibility != isAccessibilityGranted { isAccessibilityGranted = permissions.accessibility }
        if permissions.screenRecording != isScreenRecordingGranted { isScreenRecordingGranted = permissions.screenRecording }
        updateLock()
    }

    private func updateLock() {
        let unlocksAt = lock.update(arePermissionsMissing: arePermissionsMissing, activation: activation, at: Self.uptime())
        if lock.isLocked != isLocked { isLocked = lock.isLocked }
        guard let unlocksAt else {
            cancelUnlock()
            return
        }
        guard unlockTask == nil else { return }
        let delay = max(0, unlocksAt - Self.uptime())
        unlockTask = Task { [weak self] in
            try? await Task.sleep(for: .seconds(delay))
            guard !Task.isCancelled, let self else { return }
            unlockTask = nil
            updateLock()
        }
    }

    private func cancelUnlock() {
        unlockTask?.cancel()
        unlockTask = nil
    }

    private static func uptime() -> TimeInterval { ProcessInfo.processInfo.systemUptime }

    private static func systemPermissions() -> (accessibility: Bool, screenRecording: Bool) {
        (AccessibilityReader.isTrusted, Permissions.isScreenRecordingGranted)
    }
}
