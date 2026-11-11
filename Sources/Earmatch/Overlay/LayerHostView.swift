import AppKit
import QuartzCore

class LayerHostView: NSView {
    let root = CALayer()

    init() {
        super.init(frame: .zero)
        root.isGeometryFlipped = true
        root.masksToBounds = true
        layer = root
        wantsLayer = true
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) { fatalError("init(coder:) is not supported") }

    override var isFlipped: Bool { true }

    static func withoutAnimation(_ changes: () -> Void) {
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        changes()
        CATransaction.commit()
    }
}

enum OverlayColor {
    static let masterOrange = CGColor(srgbRed: 240 / 255, green: 140 / 255, blue: 40 / 255, alpha: 1)
    static let masterOrangeDim = CGColor(srgbRed: 130 / 255, green: 72 / 255, blue: 22 / 255, alpha: 1)
    static let deckBlue = CGColor(srgbRed: 60 / 255, green: 140 / 255, blue: 220 / 255, alpha: 1)
    static let deckBlueDim = CGColor(srgbRed: 30 / 255, green: 68 / 255, blue: 110 / 255, alpha: 1)
    static let downbeatRed = CGColor(srgbRed: 230 / 255, green: 50 / 255, blue: 40 / 255, alpha: 1)
    static let nowWhite = CGColor(srgbRed: 1, green: 1, blue: 1, alpha: 1)
    static let black = CGColor(srgbRed: 0, green: 0, blue: 0, alpha: 1)
    static let divider = CGColor(srgbRed: 52 / 255, green: 52 / 255, blue: 56 / 255, alpha: 1)
    static let band = CGColor(srgbRed: 0, green: 0, blue: 0, alpha: 0.82)
    static let cdjOrange = CGColor(srgbRed: 1, green: 115 / 255, blue: 0, alpha: 1)
    static let cdjBlue = CGColor(srgbRed: 0, green: 125 / 255, blue: 1, alpha: 1)
    static let tickGrey = CGColor(srgbRed: 205 / 255, green: 205 / 255, blue: 205 / 255, alpha: 1)
    static let bandEmpty = CGColor(srgbRed: 14 / 255, green: 14 / 255, blue: 16 / 255, alpha: 1)
    static let headerGrey = CGColor(srgbRed: 30 / 255, green: 30 / 255, blue: 30 / 255, alpha: 1)

    static func row(isMaster: Bool, cdj3000: Bool) -> CGColor {
        switch (isMaster, cdj3000) {
        case (true, false): masterOrange
        case (false, false): deckBlue
        case (true, true): cdjOrange
        case (false, true): cdjBlue
        }
    }
}
