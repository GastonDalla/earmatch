import AppKit
import EarmatchCore
import QuartzCore

nonisolated final class PhaseMeterLayer: CALayer {
    static let compactWidth: CGFloat = 380
    static let compactHeight: CGFloat = 34

    var rowsCenter: CGFloat = 0.45
    var playheadExtension: CGFloat = 0
    var allowsMasterWaveform = false

    private let labelTop = PhaseMeterLayer.text(size: 6.5, weight: .bold, alignment: .right)
    private let labelBottom = PhaseMeterLayer.text(size: 6.5, weight: .bold, alignment: .right)
    private let badge = CALayer()
    private let badgeNumber = PhaseMeterLayer.text(size: 11, weight: .heavy, alignment: .center)
    private let rows = [RowLayer(), RowLayer()]
    private let nowLine = CALayer()
    private let band = CALayer()
    private let bandImage = CALayer()
    private var shownSurface: IOSurfaceRef?
    private var isBandImageShown = false

    private struct Layout: Equatable {
        var bounds: CGRect
        var rowsCenter: CGFloat
        var style: PhaseMeterType
        var rowCount: Int
        var labelLines: Int
        var labelPlayer: Int?
        var usesBand: Bool
        var playheadExtension: CGFloat
        var scale: CGFloat
    }

    private var laidOut: Layout?
    private var bandRect: CGRect?

    override init() {
        super.init()
        for label in [labelTop, labelBottom] {
            label.foregroundColor = OverlayColor.masterOrange
            addSublayer(label)
        }
        labelTop.string = PhaseMeterModel.masterLabelLines.first
        labelBottom.string = PhaseMeterModel.masterLabelLines.last
        badge.cornerRadius = 2
        addSublayer(badge)
        badgeNumber.foregroundColor = OverlayColor.black
        badge.addSublayer(badgeNumber)
        band.backgroundColor = OverlayColor.bandEmpty
        band.masksToBounds = true
        bandImage.contentsGravity = .resize
        bandImage.isOpaque = true
        band.addSublayer(bandImage)
        addSublayer(band)
        rows.forEach(addSublayer)
        nowLine.backgroundColor = OverlayColor.nowWhite
        addSublayer(nowLine)
    }

    override init(layer: Any) { super.init(layer: layer) }

    @available(*, unavailable)
    required init?(coder: NSCoder) { fatalError("init(coder:) is not supported") }

    func show(_ display: PhaseMeterDisplay?, scale: CGFloat) {
        guard let display, !display.rows.isEmpty else {
            if !isHidden { isHidden = true }
            return
        }
        if isHidden { isHidden = false }
        let layout = Layout(bounds: bounds, rowsCenter: rowsCenter, style: display.style, rowCount: min(display.rows.count, rows.count),
                            labelLines: display.label?.lines.count ?? 0, labelPlayer: display.label?.player,
                            usesBand: display.style == .cdj3000Waveform && allowsMasterWaveform,
                            playheadExtension: playheadExtension, scale: scale)
        if layout != laidOut {
            apply(layout)
            laidOut = layout
        }
        for (index, rowLayer) in rows.enumerated() where index < layout.rowCount {
            rowLayer.show(display.rows[index])
        }
        let showsImage = layout.usesBand && display.showsMasterWaveform && shownSurface != nil
        if showsImage != isBandImageShown {
            isBandImageShown = showsImage
            bandImage.isHidden = !showsImage
        }
    }

    var isWaitingForMasterWaveform: Bool { bandRect != nil && shownSurface == nil && !isHidden }

    func showMasterWaveform(_ frame: FrameReading?, strip: CGRect?) {
        guard let frame, let strip, let bandRect, let surface = frame.surface,
              frame.pixelSize.width > 0, frame.pixelSize.height > 0
        else {
            if shownSurface != nil {
                bandImage.contents = nil
                shownSurface = nil
                bandImage.isHidden = true
                isBandImageShown = false
            }
            return
        }
        if shownSurface !== surface {
            bandImage.contents = surface
            shownSurface = surface
        }
        let source = CGRect(x: strip.midX - bandRect.width / 2, y: strip.minY, width: bandRect.width, height: strip.height)
        let pixels = frame.pixelRect(source)
        bandImage.contentsRect = CGRect(x: pixels.minX / frame.pixelSize.width, y: pixels.minY / frame.pixelSize.height,
                                        width: pixels.width / frame.pixelSize.width, height: pixels.height / frame.pixelSize.height)
    }

    private func apply(_ layout: Layout) {
        let style = layout.style
        let bounds = layout.bounds
        let centerX = bounds.midX
        let geometry = MeterGeometry(style: style, bounds: bounds, rowsCenter: layout.rowsCenter, rowCount: layout.rowCount,
                                     usesBand: layout.usesBand)

        for (index, rowLayer) in rows.enumerated() {
            rowLayer.isHidden = index >= layout.rowCount
            guard index < layout.rowCount else { continue }
            rowLayer.frame = bounds
            rowLayer.layout(geometry, y: geometry.rowY(index), scale: layout.scale)
        }

        band.isHidden = !layout.usesBand
        bandRect = layout.usesBand ? geometry.band : nil
        if let rect = bandRect {
            band.frame = rect
            bandImage.frame = band.bounds
        } else {
            bandImage.isHidden = true
            isBandImageShown = false
        }

        let showsLabel = layout.labelPlayer != nil
        badge.isHidden = !showsLabel
        labelTop.isHidden = !showsLabel || layout.labelLines == 0
        labelBottom.isHidden = labelTop.isHidden
        if let player = layout.labelPlayer {
            let size: CGFloat = 14
            let badgeRect: CGRect
            switch geometry.labelPlace {
            case let .leftOfMeter(x, y): badgeRect = CGRect(x: x - size, y: y - size / 2, width: size, height: size)
            case let .rightOfMeter(x, y): badgeRect = CGRect(x: x, y: y - size / 2, width: size, height: size)
            }
            badge.backgroundColor = style.isCDJ3000 ? OverlayColor.cdjOrange : OverlayColor.masterOrange
            badge.frame = badgeRect
            badgeNumber.string = "\(player)"
            badgeNumber.contentsScale = layout.scale
            badgeNumber.frame = CGRect(x: 0, y: size / 2 - 0.6 * 11 - 0.5, width: size, height: 13)
            let textRight = badgeRect.minX - 4
            for (label, offset) in [(labelTop, -3.8), (labelBottom, 3.8)] {
                label.contentsScale = layout.scale
                label.frame = CGRect(x: textRight - 44, y: badgeRect.midY + offset - 0.6 * 6.5, width: 44, height: 8)
            }
        }

        nowLine.isHidden = style == .box
        let lineTop: CGFloat
        let lineBottom: CGFloat
        if let rect = bandRect {
            lineTop = rect.minY - 3
            lineBottom = bounds.maxY + layout.playheadExtension
        } else {
            lineTop = geometry.rowY(0) - geometry.tickLength * 1.4
            lineBottom = geometry.rowY(layout.rowCount - 1) + geometry.tickLength * 1.4
        }
        let width: CGFloat = style.isCDJ3000 ? 2 : 1.5
        nowLine.frame = CGRect(x: centerX - width / 2, y: lineTop, width: width, height: lineBottom - lineTop)
    }

    static func text(size: CGFloat, weight: NSFont.Weight, alignment: CATextLayerAlignmentMode, monospacedDigits: Bool = false) -> CATextLayer {
        let layer = CATextLayer()
        let font = monospacedDigits ? NSFont.monospacedDigitSystemFont(ofSize: size, weight: weight) : NSFont.systemFont(ofSize: size, weight: weight)
        layer.font = font
        layer.fontSize = size
        layer.alignmentMode = alignment
        layer.truncationMode = .none
        layer.isWrapped = false
        return layer
    }
}

nonisolated private struct MeterGeometry {
    enum LabelPlace {
        case leftOfMeter(x: CGFloat, y: CGFloat)
        case rightOfMeter(x: CGFloat, y: CGFloat)
    }

    let style: PhaseMeterType
    let centerX: CGFloat
    let meterWidth: CGFloat
    let pitch: CGFloat
    let firstRowY: CGFloat
    let tickLength: CGFloat
    let band: CGRect?
    let labelPlace: LabelPlace

    init(style: PhaseMeterType, bounds: CGRect, rowsCenter: CGFloat, rowCount: Int, usesBand: Bool) {
        self.style = style
        centerX = bounds.midX
        switch style {
        case .box, .line:
            meterWidth = 180
            pitch = 15
        case .cdj3000PhaseMeter, .cdj3000Waveform:
            meterWidth = max(120, min(400, bounds.width - 190))
            pitch = min(18, bounds.height * 0.45)
        }
        tickLength = style.isCDJ3000 ? pitch * 0.45 : 5
        if usesBand {
            let half = max(80, min(bounds.width / 2 - 100, 320))
            let height = max(12, min(30, bounds.height - 14))
            let rect = CGRect(x: centerX - half, y: bounds.maxY - 4 - height, width: 2 * half, height: height)
            band = rect
            firstRowY = rect.midY - 8
            labelPlace = .leftOfMeter(x: rect.minX - 6, y: rect.midY - 8)
        } else {
            band = nil
            let rowsMidY = bounds.height * rowsCenter
            firstRowY = rowsMidY - CGFloat(rowCount - 1) * pitch / 2
            switch style {
            case .box, .line: labelPlace = .leftOfMeter(x: centerX - meterWidth / 2 - 8, y: rowsMidY)
            case .cdj3000PhaseMeter, .cdj3000Waveform: labelPlace = .rightOfMeter(x: centerX + meterWidth / 2 + 8, y: firstRowY)
            }
        }
    }

    var meterLeft: CGFloat { centerX - meterWidth / 2 }
    var meterRight: CGFloat { centerX + meterWidth / 2 }

    func rowY(_ index: Int) -> CGFloat {
        band != nil ? firstRowY + CGFloat(index) * 16 : firstRowY + CGFloat(index) * pitch
    }

    var counterEdge: CGFloat {
        if let band { return band.minX - 26 }
        return style.isCDJ3000 ? meterLeft - 10 : meterRight + 10
    }
}

nonisolated private final class RowLayer: CALayer {
    private static let maxTicks = Int(2 * PhaseMeterModel.cdj3000Span) + 1

    private let boxes = (0 ..< 4).map { _ in CALayer() }
    private let connectors = (0 ..< 3).map { _ in CALayer() }
    private let ruler = CALayer()
    private let ticks = (0 ..< RowLayer.maxTicks).map { _ in CALayer() }
    private let value = PhaseMeterLayer.text(size: 12, weight: .medium, alignment: .right, monospacedDigits: true)
    private let unit = PhaseMeterLayer.text(size: 7.5, weight: .semibold, alignment: .left)

    private var meterStyle = PhaseMeterType.box
    private var y: CGFloat = 0
    private var centerX: CGFloat = 0
    private var halfWidth: CGFloat = 0
    private var tickLength: CGFloat = 5
    private var showsRuler = true
    private var role: PhaseMeterRow.Role?
    private var shownBoxes: [PhaseMeterBox] = []
    private var shownCounter = ""
    private var tickXs = [CGFloat](repeating: .nan, count: RowLayer.maxTicks)
    private var tickIsDownbeat = [Bool](repeating: false, count: RowLayer.maxTicks)
    private var isDimmed = false

    override init() {
        super.init()
        connectors.forEach(addSublayer)
        boxes.forEach(addSublayer)
        addSublayer(ruler)
        ticks.forEach(addSublayer)
        addSublayer(value)
        addSublayer(unit)
        unit.string = PhaseMeterDisplay.counterUnit
    }

    override init(layer: Any) { super.init(layer: layer) }

    @available(*, unavailable)
    required init?(coder: NSCoder) { fatalError("init(coder:) is not supported") }

    fileprivate func layout(_ geometry: MeterGeometry, y: CGFloat, scale: CGFloat) {
        meterStyle = geometry.style
        self.y = y
        centerX = geometry.centerX
        halfWidth = geometry.meterWidth / 2
        tickLength = geometry.tickLength
        showsRuler = meterStyle != .box && geometry.band == nil
        let isBox = meterStyle == .box

        for box in boxes { box.isHidden = !isBox }
        for connector in connectors { connector.isHidden = !isBox }
        ruler.isHidden = !showsRuler
        for tick in ticks { tick.isHidden = true }
        for index in tickXs.indices { tickXs[index] = .nan }

        let gap: CGFloat = 6
        let boxWidth = (geometry.meterWidth - 3 * gap) / 4
        let boxHeight: CGFloat = 11
        for (index, box) in boxes.enumerated() {
            box.frame = CGRect(x: geometry.meterLeft + CGFloat(index) * (boxWidth + gap), y: y - boxHeight / 2, width: boxWidth, height: boxHeight)
        }
        for (index, connector) in connectors.enumerated() {
            connector.frame = CGRect(x: boxes[index].frame.maxX, y: y - 0.75, width: gap, height: 1.5)
        }
        let thickness: CGFloat = meterStyle.isCDJ3000 ? 2 : 1.5
        ruler.frame = CGRect(x: geometry.meterLeft, y: y - thickness / 2, width: geometry.meterWidth, height: thickness)

        let valueSize: CGFloat = meterStyle.isCDJ3000 ? 13.5 : 12
        let unitSize: CGFloat = 7.5
        value.fontSize = valueSize
        value.contentsScale = scale
        unit.contentsScale = scale
        let valueWidth: CGFloat = 44, unitWidth: CGFloat = 24
        let valueTop = y - 0.6 * valueSize
        let unitTop = valueTop + 0.95 * (valueSize - unitSize)
        let edge = geometry.counterEdge
        if meterStyle.isCDJ3000 {
            unit.frame = CGRect(x: edge - unitWidth, y: unitTop, width: unitWidth, height: unitSize + 3)
            value.frame = CGRect(x: edge - unitWidth - 3 - valueWidth, y: valueTop, width: valueWidth, height: valueSize + 4)
        } else {
            value.frame = CGRect(x: edge, y: valueTop, width: valueWidth, height: valueSize + 4)
            unit.frame = CGRect(x: edge + valueWidth + 3, y: unitTop, width: unitWidth, height: unitSize + 3)
        }
        role = nil
        shownBoxes = []
    }

    func show(_ row: PhaseMeterRow) {
        let isMaster = row.role == .master
        let colour = OverlayColor.row(isMaster: isMaster, cdj3000: meterStyle.isCDJ3000)
        if role != row.role {
            role = row.role
            ruler.backgroundColor = colour
            value.foregroundColor = colour
            unit.foregroundColor = colour
            let dim = isMaster ? OverlayColor.masterOrangeDim : OverlayColor.deckBlueDim
            for connector in connectors { connector.backgroundColor = dim }
            shownBoxes = []
            for index in tickXs.indices { tickXs[index] = .nan }
        }
        let dimmed = row.beatInBar == nil && !row.isVacant
        if dimmed != isDimmed {
            isDimmed = dimmed
            opacity = dimmed ? 0.35 : 1
        }

        if meterStyle == .box {
            if shownBoxes != row.boxes, row.boxes.count == boxes.count {
                let dim = isMaster ? OverlayColor.masterOrangeDim : OverlayColor.deckBlueDim
                for (index, box) in boxes.enumerated() {
                    Self.style(box, row.boxes[index], colour: colour, dim: dim)
                }
                shownBoxes = row.boxes
            }
        } else if showsRuler {
            showTicks(row, colour: colour)
        }

        if shownCounter != row.counter {
            shownCounter = row.counter
            value.string = row.counter
        }
    }

    private func showTicks(_ row: PhaseMeterRow, colour: CGColor) {
        let beatColour = meterStyle.isCDJ3000 ? OverlayColor.nowWhite : OverlayColor.tickGrey
        let toward: CGFloat = row.tickDirection == .up ? -1 : 1
        for (index, layer) in ticks.enumerated() {
            guard index < row.ticks.count else {
                if !layer.isHidden { layer.isHidden = true }
                continue
            }
            let tick = row.ticks[index]
            let x = centerX + CGFloat(tick.position) * halfWidth
            if layer.isHidden { layer.isHidden = false }
            guard x != tickXs[index] || tick.isDownbeat != tickIsDownbeat[index] else { continue }
            tickXs[index] = x
            tickIsDownbeat[index] = tick.isDownbeat
            if tick.isDownbeat {
                let near = y + toward * tickLength * 1.15, far = y - toward * tickLength * 0.9
                layer.frame = CGRect(x: x - 1, y: min(near, far), width: 2, height: abs(near - far))
                if layer.backgroundColor !== OverlayColor.downbeatRed { layer.backgroundColor = OverlayColor.downbeatRed }
            } else {
                let end = y + toward * tickLength
                layer.frame = CGRect(x: x - 0.75, y: min(y, end), width: 1.5, height: tickLength)
                if layer.backgroundColor !== beatColour { layer.backgroundColor = beatColour }
            }
        }
    }

    private static func style(_ box: CALayer, _ state: PhaseMeterBox, colour: CGColor, dim: CGColor) {
        switch state {
        case .idle:
            box.backgroundColor = nil
            box.borderColor = dim
            box.borderWidth = 1
        case .masterBeat:
            box.backgroundColor = nil
            box.borderColor = colour
            box.borderWidth = 2
        case .inPhase:
            box.backgroundColor = OverlayColor.deckBlue
            box.borderColor = OverlayColor.deckBlue
            box.borderWidth = 1
        case .outOfPhase:
            box.backgroundColor = OverlayColor.downbeatRed
            box.borderColor = OverlayColor.downbeatRed
            box.borderWidth = 1
        }
    }
}
