import AppKit
import QuartzCore

final class CoverView: LayerHostView {
    static let margin: CGFloat = 1

    init(color: CGColor) {
        super.init()
        root.backgroundColor = color
    }
}
