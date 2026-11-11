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

    init(store: SettingsStore, rememberedPane: String?, language: AppLanguageChoice, canCheckForUpdates: Bool) {
        let permissions = Self.systemPermissions()
        self.store = store
        self.language = language
        self.canCheckForUpdates = canCheckForUpdates
        readPermissions = Self.systemPermissions
        settings = store.settings
        isAccessibilityGranted = permissions.accessibility
        isScreenRecordingGranted = permissions.screenRecording
        pane = MainWindowPane.initial(stored: rememberedPane,
                                      arePermissionsMissing: !permissions.accessibility || !permissions.screenRecording)
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
        isVisible = true
    }

    isolated deinit {
        if let settingsObserver { NotificationCenter.default.removeObserver(settingsObserver) }
    }

    var status: StatusContext {
        StatusContext(activation: activation, settings: settings, isScreenRecordingGranted: isScreenRecordingGranted,
                      rekordboxVersion: rekordboxVersion, captureProblem: captureProblem, layoutName: layoutName)
    }

    var arePermissionsMissing: Bool { !isAccessibilityGranted || !isScreenRecordingGranted }

    var windowStatus: MainWindowStatus {
        MainWindowStatus(arePermissionsMissing: arePermissionsMissing, isEnabled: settings.isEnabled, activation: activation)
    }

    func windowDidOpen() {
        refreshPermissions()
        if arePermissionsMissing { pane = .setup }
    }

    func select(_ newPane: MainWindowPane) {
        guard newPane != pane else { return }
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
        if isVisible { refreshPermissions() }
    }

    func apply(captureProblem newProblem: String?) {
        if newProblem != captureProblem { captureProblem = newProblem }
    }

    func refreshPermissions() {
        let permissions = readPermissions()
        if permissions.accessibility != isAccessibilityGranted { isAccessibilityGranted = permissions.accessibility }
        if permissions.screenRecording != isScreenRecordingGranted { isScreenRecordingGranted = permissions.screenRecording }
    }

    private static func systemPermissions() -> (accessibility: Bool, screenRecording: Bool) {
        (AccessibilityReader.isTrusted, Permissions.isScreenRecordingGranted)
    }
}
