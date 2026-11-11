import AppKit
import SwiftUI
import EarmatchCore

final class MainWindowModel: ObservableObject {
    @Published private(set) var settings: OverlaySettings
    @Published private(set) var activation: OverlayActivation = RekordboxState.initial.activation
    @Published private(set) var rekordboxVersion: String?
    @Published private(set) var layoutName: String?
    @Published private(set) var isAccessibilityGranted = AccessibilityReader.isTrusted
    @Published private(set) var isScreenRecordingGranted = Permissions.isScreenRecordingGranted
    @Published private(set) var isPermissionsCardShown = false
    @Published var isVisible = false
    @Published private(set) var captureProblem: String?
    @Published var isKeyboardNavigating = false

    let versionLabel = SystemInfo.shortVersion.map { "v\($0)" } ?? L10n.text("development build")
    var onCopyDiagnostics: () -> Void = {}

    private let store: SettingsStore
    private var settingsObserver: NSObjectProtocol?

    init(store: SettingsStore) {
        self.store = store
        settings = store.settings
        settingsObserver = NotificationCenter.default.addObserver(forName: SettingsStore.didChange, object: store, queue: .main) { [weak self] _ in
            guard let self else { return }
            let latest = self.store.settings
            if latest != self.settings { self.settings = latest }
        }
    }

    deinit {
        if let settingsObserver { NotificationCenter.default.removeObserver(settingsObserver) }
    }

    var status: StatusContext {
        StatusContext(activation: activation, settings: settings, isScreenRecordingGranted: isScreenRecordingGranted,
                      rekordboxVersion: rekordboxVersion, captureProblem: captureProblem, layoutName: layoutName)
    }

    var arePermissionsMissing: Bool { !isAccessibilityGranted || !isScreenRecordingGranted }

    func windowDidOpen() {
        refreshPermissions()
        isPermissionsCardShown = arePermissionsMissing
    }

    func binding<Value>(_ keyPath: WritableKeyPath<OverlaySettings, Value>) -> Binding<Value> {
        Binding(
            get: { [weak self] in self?.settings[keyPath: keyPath] ?? OverlaySettings()[keyPath: keyPath] },
            set: { [weak self] value in self?.store.update { $0[keyPath: keyPath] = value } }
        )
    }

    var boothPreset: Binding<BoothPreset?> {
        Binding(
            get: { [weak self] in self.flatMap { BoothPreset.selected(in: $0.settings) } },
            set: { [weak self] preset in
                guard let preset else { return }
                self?.store.update { preset.apply(to: &$0) }
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
        let accessibility = AccessibilityReader.isTrusted
        let screenRecording = Permissions.isScreenRecordingGranted
        if accessibility != isAccessibilityGranted { isAccessibilityGranted = accessibility }
        if screenRecording != isScreenRecordingGranted { isScreenRecordingGranted = screenRecording }
        if arePermissionsMissing && !isPermissionsCardShown { isPermissionsCardShown = true }
    }
}
