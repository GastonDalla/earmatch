import AppKit
import QuartzCore

final class DisplayLinkDriver {
    var onRefresh: ((_ timestamp: CFTimeInterval, _ targetTime: CFTimeInterval) -> Void)?

    private var link: CADisplayLink?
    private var displayID: CGDirectDisplayID?
    private let proxy = Proxy()

    init() {
        proxy.driver = self
    }

    isolated deinit {
        link?.invalidate()
    }

    var isRunning: Bool { link.map { !$0.isPaused } ?? false }

    func run(on screen: NSScreen?) {
        guard let screen, let id = screen.displayID else {
            stop()
            return
        }
        if id != displayID {
            link?.invalidate()
            let link = screen.displayLink(target: proxy, selector: #selector(Proxy.step(_:)))
            let fastest = Float(max(screen.maximumFramesPerSecond, 60))
            link.preferredFrameRateRange = CAFrameRateRange(minimum: min(60, fastest), maximum: fastest, preferred: fastest)
            link.add(to: .main, forMode: .common)
            self.link = link
            displayID = id
        }
        link?.isPaused = false
    }

    func stop() {
        link?.isPaused = true
    }

    fileprivate func step(_ link: CADisplayLink) {
        onRefresh?(link.timestamp, link.targetTimestamp)
    }

    static func screen(showingMostOf quartzRect: CGRect) -> NSScreen? {
        let cocoa = ScreenGeometry.cocoaRect(fromQuartz: quartzRect)
        func shown(_ screen: NSScreen) -> CGFloat {
            let overlap = screen.frame.intersection(cocoa)
            return overlap.isNull ? 0 : overlap.width * overlap.height
        }
        return NSScreen.screens.max { shown($0) < shown($1) }
    }

    private final class Proxy: NSObject {
        weak var driver: DisplayLinkDriver?

        @objc func step(_ link: CADisplayLink) {
            driver?.step(link)
        }
    }
}

private extension NSScreen {
    var displayID: CGDirectDisplayID? {
        (deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber)?.uint32Value
    }
}
