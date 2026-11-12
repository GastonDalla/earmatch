// Draws the background of the Earmatch DMG window: the title, a well for each icon, the waveform arrow from
// the app to the Applications folder, and the light strips that Finder's icon labels sit on.
//
// Usage: swift scripts/dmg-background.swift OUTPUT_DIR
// Writes OUTPUT_DIR/background.png (640x400) and OUTPUT_DIR/background@2x.png (1280x800). release.sh joins
// them into one HiDPI TIFF with tiffutil. The icon positions here must match scripts/dmg-layout.applescript.
import AppKit

let size = CGSize(width: 640, height: 400)
let appCenter = CGPoint(x: 160, y: 200)
let applicationsCenter = CGPoint(x: 480, y: 200)

// Finder draws the icon labels of a window with a background picture in black, in dark mode too (checked on
// macOS 27 in Dark), so each well ends in a light strip under its label, like the board tape DJs write mixer
// channel names on. Black on it reads at about 15:1. Finder centres a 13-point label about 86 points under the
// centre of a 128-point icon.
let tapeColor = NSColor(srgbRed: 0.855, green: 0.847, blue: 0.812, alpha: 1)
let labelCenterOffset: CGFloat = 86
let wellHalfWidth: CGFloat = 92
let accent = NSColor(srgbRed: 0.72, green: 0.95, blue: 0.23, alpha: 1)
let primaryText = NSColor(white: 1, alpha: 0.92)
let secondaryText = NSColor(white: 1, alpha: 0.56)
let quietText = NSColor(white: 1, alpha: 0.32)

guard CommandLine.arguments.count == 2 else {
    FileHandle.standardError.write(Data("usage: swift scripts/dmg-background.swift OUTPUT_DIR\n".utf8))
    exit(2)
}
let outputDirectory = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)

/// Converts a point measured from the top of the window (as Finder places icons) to drawing coordinates.
func top(_ y: CGFloat) -> CGFloat { size.height - y }

func drawCentered(_ text: String, font: NSFont, color: NSColor, centerX: CGFloat, topY: CGFloat, kern: CGFloat = 0) {
    let attributes: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: color, .kern: kern]
    let string = NSAttributedString(string: text, attributes: attributes)
    let bounds = string.size()
    string.draw(at: CGPoint(x: centerX - bounds.width / 2, y: top(topY) - bounds.height))
}

func drawBackground() {
    let gradient = NSGradient(colors: [
        NSColor(srgbRed: 0.118, green: 0.118, blue: 0.129, alpha: 1),
        NSColor(srgbRed: 0.075, green: 0.075, blue: 0.082, alpha: 1),
    ])!
    gradient.draw(in: CGRect(origin: .zero, size: size), angle: -90)

    // Two shallow wells the icons sit in, like the channels of a two-channel mixer.
    for center in [appCenter, applicationsCenter] {
        let wellTop = center.y - wellHalfWidth
        let wellBottom = center.y + labelCenterOffset + 18
        let well = CGRect(x: center.x - wellHalfWidth, y: top(wellBottom), width: 2 * wellHalfWidth, height: wellBottom - wellTop)
        let path = NSBezierPath(roundedRect: well, xRadius: 28, yRadius: 28)
        NSColor(white: 1, alpha: 0.035).setFill()
        path.fill()
        NSColor(white: 1, alpha: 0.07).setStroke()
        path.lineWidth = 1
        path.stroke()
    }
}

func drawTapeStrips() {
    for center in [appCenter, applicationsCenter] {
        let height: CGFloat = 24
        let labelCenter = center.y + labelCenterOffset
        let strip = CGRect(x: center.x - wellHalfWidth + 14, y: top(labelCenter) - height / 2,
                           width: 2 * (wellHalfWidth - 14), height: height)
        tapeColor.setFill()
        NSBezierPath(roundedRect: strip, xRadius: 3, yRadius: 3).fill()
    }
}

/// A waveform that grows towards the Applications folder and ends in a chevron: the drag direction.
func drawArrow() {
    let startX = appCenter.x + 96
    let endX = applicationsCenter.x - 96
    let midY = top(appCenter.y)
    let chevronWidth: CGFloat = 16
    let barsEnd = endX - chevronWidth - 12
    let barWidth: CGFloat = 3.5
    let step: CGFloat = 8
    let count = Int((barsEnd - startX) / step) + 1
    let shape: [CGFloat] = [0.30, 0.55, 0.40, 0.75, 0.50, 0.95, 0.60, 0.85, 0.45, 1.0, 0.70, 0.90, 0.55, 0.80]
    for index in 0..<count {
        let progress = CGFloat(index) / CGFloat(max(count - 1, 1))
        let height = 6 + 30 * shape[index % shape.count] * (0.55 + 0.45 * progress)
        let x = startX + CGFloat(index) * step
        let bar = CGRect(x: x - barWidth / 2, y: midY - height / 2, width: barWidth, height: height)
        accent.withAlphaComponent(0.35 + 0.65 * progress).setFill()
        NSBezierPath(roundedRect: bar, xRadius: barWidth / 2, yRadius: barWidth / 2).fill()
    }
    let chevron = NSBezierPath()
    chevron.move(to: CGPoint(x: endX - chevronWidth, y: midY + 14))
    chevron.line(to: CGPoint(x: endX, y: midY))
    chevron.line(to: CGPoint(x: endX - chevronWidth, y: midY - 14))
    chevron.lineWidth = 4
    chevron.lineCapStyle = .round
    chevron.lineJoinStyle = .round
    accent.setStroke()
    chevron.stroke()
}

func drawText() {
    let centerX = size.width / 2
    drawCentered("Drag Earmatch to Applications", font: .systemFont(ofSize: 21, weight: .semibold),
                 color: primaryText, centerX: centerX, topY: 38)
    drawCentered("Then open it from Applications and allow its two permissions.", font: .systemFont(ofSize: 13),
                 color: secondaryText, centerX: centerX, topY: 68)
    drawCentered("FOR REKORDBOX 7  ·  MACOS 14 OR LATER", font: .monospacedSystemFont(ofSize: 10, weight: .semibold),
                 color: quietText, centerX: centerX, topY: 362, kern: 1.6)
}

func render(scale: CGFloat) throws -> Data {
    let pixelsWide = Int(size.width * scale)
    let pixelsHigh = Int(size.height * scale)
    guard let rep = NSBitmapImageRep(
        bitmapDataPlanes: nil, pixelsWide: pixelsWide, pixelsHigh: pixelsHigh, bitsPerSample: 8, samplesPerPixel: 4,
        hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0
    ) else { throw CocoaError(.fileWriteUnknown) }
    rep.size = size
    let srgb = rep.retagging(with: .sRGB) ?? rep
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: srgb)
    drawBackground()
    drawTapeStrips()
    drawArrow()
    drawText()
    NSGraphicsContext.restoreGraphicsState()
    guard let png = srgb.representation(using: .png, properties: [:]) else { throw CocoaError(.fileWriteUnknown) }
    return png
}

do {
    try FileManager.default.createDirectory(at: outputDirectory, withIntermediateDirectories: true)
    try render(scale: 1).write(to: outputDirectory.appending(path: "background.png"))
    try render(scale: 2).write(to: outputDirectory.appending(path: "background@2x.png"))
} catch {
    FileHandle.standardError.write(Data("error: could not draw the DMG background: \(error)\n".utf8))
    exit(1)
}
