import CoreGraphics
import Foundation
import ImageIO
import Testing
import EarmatchCore

final class PixelBuffer {
    let width: Int
    let height: Int
    var bytes: [UInt8]

    init(width: Int, height: Int, fill: (UInt8, UInt8, UInt8) = (0, 0, 0)) {
        self.width = width
        self.height = height
        bytes = [UInt8](repeating: 255, count: width * height * 4)
        for i in 0 ..< width * height {
            bytes[i * 4] = fill.0; bytes[i * 4 + 1] = fill.1; bytes[i * 4 + 2] = fill.2
        }
    }

    init(png name: String) throws {
        let url = try Fixture.url(name)
        let source = try #require(CGImageSourceCreateWithURL(url as CFURL, nil))
        let image = try #require(CGImageSourceCreateImageAtIndex(source, 0, nil))
        width = image.width
        height = image.height
        bytes = [UInt8](repeating: 0, count: width * height * 4)
        let space = image.colorSpace ?? CGColorSpaceCreateDeviceRGB()
        bytes.withUnsafeMutableBytes { raw in
            let context = CGContext(data: raw.baseAddress, width: width, height: height, bitsPerComponent: 8, bytesPerRow: width * 4,
                                    space: space, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
            context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
        }
    }

    func paint(x: Int, y: Int, width w: Int, height h: Int, _ color: (UInt8, UInt8, UInt8)) {
        for row in max(0, y) ..< min(height, y + h) {
            for column in max(0, x) ..< min(width, x + w) {
                let i = (row * width + column) * 4
                bytes[i] = color.0; bytes[i + 1] = color.1; bytes[i + 2] = color.2
            }
        }
    }

    func withImage<T>(scale: CGFloat = 2, origin: CGPoint = .zero, _ body: (PixelImage) throws -> T) rethrows -> T {
        try bytes.withUnsafeBytes { raw in
            try body(PixelImage(base: raw.baseAddress!, width: width, height: height, bytesPerRow: width * 4,
                                layout: .rgba, scale: scale, origin: origin))
        }
    }
}
