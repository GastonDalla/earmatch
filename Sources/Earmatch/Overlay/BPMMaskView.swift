import AppKit
import QuartzCore

final class BPMMaskView: LayerHostView {
    static let margin: CGFloat = 3
    private let cover = CALayer()

    override init() {
        super.init()
        cover.backgroundColor = OverlayColor.black
        root.addSublayer(cover)
    }

    func show(cover rect: CGRect, viewOrigin: CGPoint) {
        let local = rect.offsetBy(dx: -viewOrigin.x, dy: -viewOrigin.y)
        guard cover.frame != local else { return }
        Self.withoutAnimation { cover.frame = local }
    }
}
