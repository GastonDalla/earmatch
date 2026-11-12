import AppKit
import EarmatchCore

final class MenuBarController: NSObject, NSMenuDelegate {
    var onOpenMainWindow: (() -> Void)?
    var onCopyDiagnostics: (() -> Void)?

    private let store: SettingsStore
    private let monitor: RekordboxMonitor
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private let menu = NSMenu()

    init(store: SettingsStore, monitor: RekordboxMonitor) {
        self.store = store
        self.monitor = monitor
        super.init()
        refreshIcon(isActive: false)
        menu.delegate = self
        statusItem.menu = menu
    }

    func refreshIcon(isActive: Bool) {
        let name = isActive ? "waveform.circle.fill" : "waveform.circle"
        statusItem.button?.image = NSImage(systemSymbolName: name, accessibilityDescription: "Earmatch")
    }

    func menuNeedsUpdate(_ menu: NSMenu) {
        menu.removeAllItems()
        let settings = store.settings

        menu.addItem(note(monitor.state.activation.message))
        if monitor.state.activation.isPermissionMissing {
            addLockedItems(to: menu)
            return
        }
        menu.addItem(.separator())

        menu.addItem(item(L10n.text("Enable Earmatch"), checked: settings.isEnabled) { $0.isEnabled.toggle() })
        menu.addItem(.separator())

        menu.addItem(submenu(OptionText.boothMenuTitle, options: BoothPreset.allCases.map { ($0.displayName, Optional($0)) },
                             selected: BoothPreset.selected(in: settings)) { settings, preset in preset?.apply(to: &settings) })
        menu.addItem(item(L10n.text("Limit BPM Display"), checked: settings.isBPMLimitEnabled) { $0.isBPMLimitEnabled.toggle() })
        menu.addItem(submenu(L10n.text("BPM Precision"), options: [
            (L10n.text("One Decimal"), .oneDecimal), (L10n.text("No Decimals"), .noDecimals), (L10n.text("Cover Fully"), .coverFully),
        ], selected: settings.bpmLimit) { $0.bpmLimit = $1 })
        menu.addItem(item(OptionText.blindModeMenuTitle, checked: settings.isBlindModeEnabled) { $0.isBlindModeEnabled.toggle() })
        menu.addItem(submenu(L10n.text("Waveform"), options: WaveformMode.allCases.map { ($0.displayName, $0) },
                             selected: settings.waveformMode) { $0.waveformMode = $1 })
        menu.addItem(item(L10n.text("Phase Meter"), checked: settings.isPhaseMeterEnabled) { $0.isPhaseMeterEnabled.toggle() })
        menu.addItem(submenu(OptionText.phaseMeterStyleMenuTitle, options: PhaseMeterType.allCases.map { ($0.title, $0) },
                             selected: settings.phaseMeterType) { $0.phaseMeterType = $1 })
        menu.addItem(submenu(OptionText.counterMenuTitle, options: CounterStyle.allCases.map { ($0.title, $0) },
                             selected: settings.counterStyle) { $0.counterStyle = $1 })
        menu.addItem(item(OptionText.mixTrainerMenuTitle, checked: settings.isMixTrainerEnabled) { $0.isMixTrainerEnabled.toggle() })

        menu.addItem(.separator())
        menu.addItem(ClosureMenuItem(title: L10n.text("Open Earmatch…")) { [weak self] in self?.onOpenMainWindow?() })
        menu.addItem(ClosureMenuItem(title: L10n.text("Copy Diagnostics")) { [weak self] in self?.onCopyDiagnostics?() })
        menu.addItem(note(L10n.text("Hold Right Option to peek · ⌃⌥⌘E turns Earmatch on or off")))
        menu.addItem(.separator())
        menu.addItem(ClosureMenuItem(title: L10n.text("About Earmatch")) { AboutPanel.show() })
        if let updates = AppUpdater.shared.makeMenuItem() { menu.addItem(updates) }
        menu.addItem(ClosureMenuItem(title: Credit.menuLine) { Credit.openProfile() })
        menu.addItem(note(L10n.text("Independent utility, not affiliated with AlphaTheta, Pioneer DJ or rekordbox")))
        menu.addItem(withTitle: L10n.text("Quit Earmatch"), action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
    }

    /// Until both permissions are allowed the menu only asks for them: no option can be changed yet.
    private func addLockedItems(to menu: NSMenu) {
        if !AccessibilityReader.isTrusted {
            menu.addItem(ClosureMenuItem(title: L10n.text("Allow Accessibility…")) { Permissions.requestAccessibility() })
        }
        if !Permissions.isScreenRecordingGranted {
            menu.addItem(ClosureMenuItem(title: L10n.text("Allow Screen Recording…")) { Permissions.requestScreenRecording() })
        }
        menu.addItem(.separator())
        menu.addItem(ClosureMenuItem(title: L10n.text("Open Earmatch…")) { [weak self] in self?.onOpenMainWindow?() })
        menu.addItem(.separator())
        menu.addItem(withTitle: L10n.text("Quit Earmatch"), action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
    }

    private func note(_ title: String) -> NSMenuItem {
        let item = NSMenuItem(title: title, action: nil, keyEquivalent: "")
        item.isEnabled = false
        return item
    }

    private func item(_ title: String, checked: Bool, change: @escaping (inout OverlaySettings) -> Void) -> NSMenuItem {
        let item = ClosureMenuItem(title: title) { [store] in store.update(change) }
        item.state = checked ? .on : .off
        return item
    }

    private func submenu<Value: Equatable>(_ title: String, options: [(String, Value)], selected: Value,
                                           apply: @escaping (inout OverlaySettings, Value) -> Void) -> NSMenuItem {
        let parent = NSMenuItem(title: title, action: nil, keyEquivalent: "")
        let sub = NSMenu(title: title)
        for (label, value) in options {
            sub.addItem(item(label, checked: value == selected) { apply(&$0, value) })
        }
        parent.submenu = sub
        return parent
    }
}

nonisolated final class ClosureMenuItem: NSMenuItem {
    private let handler: @MainActor () -> Void

    init(title: String, handler: @escaping @MainActor () -> Void) {
        self.handler = handler
        super.init(title: title, action: #selector(run), keyEquivalent: "")
        target = self
    }

    @available(*, unavailable)
    required init(coder: NSCoder) { fatalError("init(coder:) is not supported") }

    @MainActor @objc private func run() { handler() }
}
