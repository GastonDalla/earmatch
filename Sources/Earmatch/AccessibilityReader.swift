import AppKit
import ApplicationServices
import EarmatchCore

struct AccessibilityStall: Error {}

final class AccessibilityReader {
    static let timeout: Float = 0.25

    static func applyGlobalTimeout() {
        AXUIElementSetMessagingTimeout(AXUIElementCreateSystemWide(), timeout)
    }

    static let rekordboxBundleID = "com.pioneerdj.rekordboxdj"

    private static let attributes = [
        kAXRoleAttribute, kAXTitleAttribute, kAXValueAttribute, kAXHelpAttribute,
        kAXPositionAttribute, kAXSizeAttribute, kAXChildrenAttribute,
    ] as CFArray
    private static let maxElements = 3000
    private static let maxDepth = 8

    static var isTrusted: Bool { AXIsProcessTrusted() }

    static func requestTrust() {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)
    }

    static func runningRekordbox() -> NSRunningApplication? {
        NSRunningApplication.runningApplications(withBundleIdentifier: rekordboxBundleID).first { !$0.isTerminated }
    }

    struct MainWindowRead {
        let window: AXUIElement
        let snapshot: RekordboxWindowSnapshot
        let elementRefs: [AXUIElement]
    }

    static func readMainWindow(pid: pid_t) throws -> MainWindowRead? {
        let app = AXUIElementCreateApplication(pid)
        AXUIElementSetMessagingTimeout(app, timeout)
        guard let windows = try copy(app, kAXWindowsAttribute) as? [AXUIElement] else { return nil }
        for window in windows {
            let values = try read(window)
            guard values.subrole == kAXStandardWindowSubrole as String, values.title == "rekordbox",
                  let windowFrame = values.frame
            else { continue }
            var elements: [AXElementSnapshot] = []
            var refs: [AXUIElement] = []
            try collect(values.children, origin: windowFrame.origin, depth: 0, into: &elements, refs: &refs)
            return MainWindowRead(window: window, snapshot: RekordboxWindowSnapshot(windowFrame: windowFrame, elements: elements), elementRefs: refs)
        }
        return nil
    }

    static func snapshot(pid: pid_t) throws -> RekordboxWindowSnapshot? {
        try readMainWindow(pid: pid)?.snapshot
    }

    static func frame(of window: AXUIElement) throws -> CGRect? {
        guard let position = try copy(window, kAXPositionAttribute).flatMap(point),
              let size = try copy(window, kAXSizeAttribute).flatMap(size)
        else { return nil }
        return CGRect(origin: position, size: size)
    }

    static func stringValue(of element: AXUIElement) throws -> String? {
        try copy(element, kAXValueAttribute) as? String
    }

    static func mainWindowIsFocused(pid: pid_t) throws -> Bool {
        let app = AXUIElementCreateApplication(pid)
        AXUIElementSetMessagingTimeout(app, timeout)
        guard let focused = try copy(app, kAXFocusedWindowAttribute) else { return false }
        let values = try read(focused as! AXUIElement)
        return values.subrole == kAXStandardWindowSubrole as String && values.title == "rekordbox"
    }

    private static func collect(_ children: [AXUIElement], origin: CGPoint, depth: Int, into elements: inout [AXElementSnapshot], refs: inout [AXUIElement]) throws {
        guard depth < maxDepth else { return }
        for child in children where elements.count < maxElements {
            let values = try read(child)
            if let role = values.role, let frame = values.frame {
                elements.append(AXElementSnapshot(
                    role: role,
                    title: values.title,
                    value: values.value,
                    help: values.help,
                    frame: frame.offsetBy(dx: -origin.x, dy: -origin.y)
                ))
                refs.append(child)
            }
            try collect(values.children, origin: origin, depth: depth + 1, into: &elements, refs: &refs)
        }
    }

    private struct Values {
        var role: String?
        var subrole: String?
        var title: String?
        var value: String?
        var help: String?
        var frame: CGRect?
        var children: [AXUIElement] = []
    }

    private static func read(_ element: AXUIElement) throws -> Values {
        var result = Values()
        var array: CFArray?
        let error = AXUIElementCopyMultipleAttributeValues(element, attributes, AXCopyMultipleAttributeOptions(rawValue: 0), &array)
        if error == .cannotComplete { throw AccessibilityStall() }
        guard error == .success, let values = array as? [AnyObject], values.count == 7 else { return result }
        result.role = nonEmptyString(values[0])
        result.title = nonEmptyString(values[1])
        result.value = nonEmptyString(values[2])
        result.help = nonEmptyString(values[3])
        if let position = point(values[4]), let size = size(values[5]) {
            result.frame = CGRect(origin: position, size: size)
        }
        result.children = (values[6] as? [AXUIElement]) ?? []
        if result.role == kAXWindowRole as String {
            result.subrole = try copy(element, kAXSubroleAttribute) as? String
        }
        return result
    }

    private static func copy(_ element: AXUIElement, _ attribute: String) throws -> AnyObject? {
        var value: AnyObject?
        let error = AXUIElementCopyAttributeValue(element, attribute as CFString, &value)
        if error == .cannotComplete { throw AccessibilityStall() }
        return error == .success ? value : nil
    }

    private static func nonEmptyString(_ value: AnyObject) -> String? {
        guard let string = value as? String, !string.isEmpty else { return nil }
        return string
    }

    private static func point(_ value: AnyObject) -> CGPoint? {
        guard CFGetTypeID(value) == AXValueGetTypeID() else { return nil }
        var point = CGPoint.zero
        return AXValueGetValue(value as! AXValue, .cgPoint, &point) ? point : nil
    }

    private static func size(_ value: AnyObject) -> CGSize? {
        guard CFGetTypeID(value) == AXValueGetTypeID() else { return nil }
        var size = CGSize.zero
        return AXValueGetValue(value as! AXValue, .cgSize, &size) ? size : nil
    }
}
