import AppKit

final class OverlayPanel: NSPanel {
    init(contentView: NSView) {
        super.init(contentRect: .zero, styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: true)
        self.contentView = contentView
        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        ignoresMouseEvents = true
        level = .floating
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]
        isReleasedWhenClosed = false
        hidesOnDeactivate = false
        animationBehavior = .none
    }

    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }

    func place(_ rectInWindow: CGRect, windowFrame: CGRect) {
        let quartz = rectInWindow.offsetBy(dx: windowFrame.minX, dy: windowFrame.minY)
        let frame = ScreenGeometry.cocoaRect(fromQuartz: quartz)
        if self.frame != frame { setFrame(frame, display: true) }
        if !isVisible { orderFrontRegardless() }
    }
}

enum ScreenGeometry {
    static func cocoaRect(fromQuartz rect: CGRect) -> CGRect {
        let primaryHeight = NSScreen.screens.first?.frame.height ?? 0
        return CGRect(x: rect.minX, y: primaryHeight - rect.maxY, width: rect.width, height: rect.height)
    }
}
