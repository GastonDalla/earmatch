import AppKit
import EarmatchCore

final class HotkeyMonitor {
    var onPeekChanged: (Bool) -> Void = { _ in }
    var onToggleEnabled: () -> Void = {}

    var isPeeking: Bool { recognizer.isPeeking }

    private static let events: NSEvent.EventTypeMask = [.flagsChanged, .keyDown]
    private static let trustCheckInterval: TimeInterval = 1

    private var recognizer = HotkeyRecognizer()
    private var localMonitor: Any?
    private var globalMonitor: Any?
    private var globalMonitorIsTrusted = false
    private var trustTimer: Timer?
    private var activationObserver: NSObjectProtocol?

    func start() {
        guard localMonitor == nil else { return }
        localMonitor = NSEvent.addLocalMonitorForEvents(matching: Self.events) { [weak self] event in
            self?.handle(event)
            return event
        }
        installGlobalMonitor()
        activationObserver = NotificationCenter.default.addObserver(
            forName: NSApplication.didBecomeActiveNotification, object: nil, queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated { self?.checkTrust() }
        }
    }

    func stop() {
        guard localMonitor != nil else { return }
        [localMonitor, globalMonitor].compactMap { $0 }.forEach(NSEvent.removeMonitor)
        localMonitor = nil
        globalMonitor = nil
        trustTimer?.invalidate()
        trustTimer = nil
        if let activationObserver { NotificationCenter.default.removeObserver(activationObserver) }
        activationObserver = nil
        apply(recognizer.reset())
    }

    private func installGlobalMonitor() {
        if let globalMonitor { NSEvent.removeMonitor(globalMonitor) }
        globalMonitorIsTrusted = AccessibilityReader.isTrusted
        globalMonitor = NSEvent.addGlobalMonitorForEvents(matching: Self.events) { [weak self] event in self?.handle(event) }
        if globalMonitorIsTrusted {
            trustTimer?.invalidate()
            trustTimer = nil
        } else if trustTimer == nil {
            let timer = Timer(timeInterval: Self.trustCheckInterval, repeats: true) { [weak self] _ in
                MainActor.assumeIsolated { self?.checkTrust() }
            }
            RunLoop.main.add(timer, forMode: .common)
            trustTimer = timer
        }
    }

    private func checkTrust() {
        guard localMonitor != nil, AccessibilityReader.isTrusted != globalMonitorIsTrusted else { return }
        installGlobalMonitor()
    }

    private func handle(_ event: NSEvent) {
        let modifiers = HotkeyRecognizer.Modifiers(rawValue: event.modifierFlags.rawValue)
        switch event.type {
        case .flagsChanged:
            apply(recognizer.handle(.flagsChanged(keyCode: event.keyCode, modifiers: modifiers)))
        case .keyDown:
            apply(recognizer.handle(.keyDown(characters: event.charactersIgnoringModifiers, modifiers: modifiers,
                                             isRepeat: event.isARepeat)))
        default:
            break
        }
    }

    private func apply(_ outcome: HotkeyRecognizer.Outcome) {
        if let peek = outcome.peek { onPeekChanged(peek) }
        if outcome.togglesEnabled { onToggleEnabled() }
    }
}
