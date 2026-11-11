import CoreGraphics

public struct PixelImage {
    public enum Layout: Sendable {
        case bgra
        case rgba
    }

    public struct RGB: Equatable, Sendable {
        public let r: UInt8
        public let g: UInt8
        public let b: UInt8
    }

    public let width: Int
    public let height: Int
    public let bytesPerRow: Int
    public let layout: Layout
    public let scale: CGFloat
    public let origin: CGPoint
    private let base: UnsafeRawPointer

    public init(base: UnsafeRawPointer, width: Int, height: Int, bytesPerRow: Int, layout: Layout, scale: CGFloat, origin: CGPoint) {
        self.base = base
        self.width = width
        self.height = height
        self.bytesPerRow = bytesPerRow
        self.layout = layout
        self.scale = scale
        self.origin = origin
    }

    @inline(__always)
    public func pixel(_ x: Int, _ y: Int) -> RGB {
        assert(x >= 0 && x < width && y >= 0 && y < height, "pixel (\(x), \(y)) outside \(width)x\(height)")
        let p = base.advanced(by: y * bytesPerRow + x * 4).assumingMemoryBound(to: UInt8.self)
        switch layout {
        case .bgra: return RGB(r: p[2], g: p[1], b: p[0])
        case .rgba: return RGB(r: p[0], g: p[1], b: p[2])
        }
    }

    public func column(_ x: CGFloat) -> Int { clamp(Int(((x - origin.x) * scale).rounded(.down)), width) }
    public func row(_ y: CGFloat) -> Int { clamp(Int(((y - origin.y) * scale).rounded(.down)), height) }
    public func pointX(_ column: Double) -> Double { Double(origin.x) + column / Double(scale) }
    public func pointY(_ row: Double) -> Double { Double(origin.y) + row / Double(scale) }

    private func clamp(_ value: Int, _ limit: Int) -> Int { min(max(value, 0), limit) }
}

extension PixelImage.RGB {
    var isMarkerRed: Bool { r >= 180 && g <= 90 && b <= 90 }

    var isTickGrey: Bool {
        let (r, g, b) = (Int(r), Int(g), Int(b))
        return abs(r - g) <= 14 && abs(g - b) <= 14 && (95 ... 215).contains(r)
    }

    var isPlayheadGrey: Bool {
        let (r, g, b) = (Int(r), Int(g), Int(b))
        return abs(r - g) <= 6 && abs(g - b) <= 6 && (100 ... 124).contains(r)
    }

    var isMasterBlue: Bool { b >= 170 && r <= 100 && (60 ... 180).contains(g) }
}
