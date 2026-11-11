import CoreGraphics

public struct BPMGlyphs: Equatable, Sendable {
    public let integerDigits: [CGRect]
    public let point: CGRect
    public let decimals: [CGRect]
}

public enum BPMGlyphMask {
    public static func cover(_ limit: BPMLimit, jogFrame: CGRect, image: PixelImage) -> CGRect {
        guard limit != .coverFully, let glyphs = glyphs(in: jogFrame, image: image), let last = glyphs.decimals.last else {
            return jogFrame
        }
        let left: CGFloat
        switch limit {
        case .oneDecimal:
            let previous = glyphs.decimals.count >= 2 ? glyphs.decimals[glyphs.decimals.count - 2] : glyphs.point
            left = (previous.maxX + last.minX) / 2
        case .noDecimals, .coverFully:
            left = (glyphs.integerDigits.last!.maxX + glyphs.point.minX) / 2
        }
        let right = max(jogFrame.maxX, last.maxX + 1)
        return CGRect(x: left, y: jogFrame.minY, width: right - left, height: jogFrame.height)
    }

    public static func glyphs(in jogFrame: CGRect, image: PixelImage) -> BPMGlyphs? {
        let clusters = inkClusters(in: jogFrame, image: image)
        guard let tallest = clusters.map(\.height).max(), tallest >= 4,
              let capTop = clusters.map(\.top).min()
        else { return nil }
        let isLarge = { (cluster: Cluster) in Double(cluster.top - capTop) <= 0.25 * Double(tallest) }
        guard let lastLarge = clusters.lastIndex(where: isLarge), clusters[...lastLarge].allSatisfy(isLarge) else { return nil }
        let rest = clusters[(lastLarge + 1)...]
        guard let point = rest.first, Double(point.height) <= 0.35 * Double(tallest) else { return nil }
        let decimals = rest.dropFirst()
        guard !decimals.isEmpty, decimals.allSatisfy({ !isLarge($0) && Double($0.height) > 0.35 * Double(tallest) }) else { return nil }
        return BPMGlyphs(
            integerDigits: clusters[...lastLarge].map { $0.rect(in: image) },
            point: point.rect(in: image),
            decimals: decimals.map { $0.rect(in: image) }
        )
    }

    struct Cluster {
        var first: Int
        var last: Int
        var top: Int
        var bottom: Int
        var height: Int { bottom - top + 1 }

        func rect(in image: PixelImage) -> CGRect {
            let minX = image.pointX(Double(first)), maxX = image.pointX(Double(last + 1))
            let minY = image.pointY(Double(top)), maxY = image.pointY(Double(bottom + 1))
            return CGRect(x: minX, y: minY, width: maxX - minX, height: maxY - minY)
        }
    }

    static func inkClusters(in frame: CGRect, image: PixelImage) -> [Cluster] {
        let left = image.column(frame.minX), right = image.column(frame.maxX)
        let top = image.row(frame.minY), bottom = image.row(frame.maxY)
        guard right > left, bottom > top else { return [] }
        var clusters: [Cluster] = []
        var current: Cluster?
        for x in left ..< right {
            var inkTop = -1, inkBottom = -1
            for y in top ..< bottom where image.pixel(x, y).isTextInk {
                if inkTop < 0 { inkTop = y }
                inkBottom = y
            }
            if inkTop >= 0 {
                if var cluster = current {
                    cluster.last = x
                    cluster.top = min(cluster.top, inkTop)
                    cluster.bottom = max(cluster.bottom, inkBottom)
                    current = cluster
                } else {
                    current = Cluster(first: x, last: x, top: inkTop, bottom: inkBottom)
                }
            } else if let cluster = current {
                clusters.append(cluster)
                current = nil
            }
        }
        if let cluster = current { clusters.append(cluster) }
        return clusters
    }
}

extension PixelImage.RGB {
    var isTextInk: Bool { max(r, g, b) >= 100 }
}
