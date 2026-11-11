import CoreGraphics
import Foundation

public struct BeatGridReading: Equatable, Sendable {
    public let beatSpacing: Double
    public let beatXs: [Double]
    public let downbeatXs: [Double]
    public let phaseInBar: Double?

    public init(beatSpacing: Double, beatXs: [Double], downbeatXs: [Double], phaseInBar: Double?) {
        self.beatSpacing = beatSpacing
        self.beatXs = beatXs
        self.downbeatXs = downbeatXs
        self.phaseInBar = phaseInBar
    }
}

public enum BeatGridDetector {
    public static let beatsPerBar = 4.0

    static let bandTop: CGFloat = 0.75
    static let bandBottom: CGFloat = 4.5
    static let clippedMinimum = 3.0

    public static func read(strip: CGRect, playheadX: CGFloat, in image: PixelImage) -> BeatGridReading? {
        read(strip: WaveformStrip(frame: strip, orientation: .horizontal), playhead: playheadX, in: image)
    }

    public static func read(strip: WaveformStrip, playhead: CGFloat, in image: PixelImage) -> BeatGridReading? {
        let markers = findMarkers(strip: strip, playhead: playhead, in: image)
        guard markers.count >= 3, let grid = fitGrid(markers, near: Double(playhead)) else { return nil }
        let onGrid = markers.filter { grid.fits($0.x) }
        let downbeats = onGrid.filter(\.isDownbeat).map(\.x)
        return BeatGridReading(
            beatSpacing: grid.spacing,
            beatXs: onGrid.map(\.x),
            downbeatXs: downbeats,
            phaseInBar: grid.phase(at: Double(playhead), downbeats: downbeats)
        )
    }

    struct Marker {
        let x: Double
        let isDownbeat: Bool
    }

    static func findMarkers(strip: WaveformStrip, playhead: CGFloat, in image: PixelImage) -> [Marker] {
        let area = strip.markerArea
        let isVertical = strip.orientation == .vertical
        let lines: Range<Int>
        let playheadLine: Double
        if isVertical {
            lines = image.row(area.minY) ..< image.row(area.maxY)
            playheadLine = Double((playhead - image.origin.y) * image.scale)
        } else {
            lines = image.column(area.minX) ..< image.column(area.maxX)
            playheadLine = Double((playhead - image.origin.x) * image.scale)
        }
        let near: Range<Int>, far: Range<Int>
        if isVertical {
            near = image.column(area.minX + bandTop) ..< image.column(area.minX + bandBottom)
            far = image.column(area.maxX - bandBottom) ..< image.column(area.maxX - bandTop)
        } else {
            near = image.row(area.minY + bandTop) ..< image.row(area.minY + bandBottom)
            far = 0 ..< 0
        }
        guard !lines.isEmpty, !near.isEmpty else { return [] }

        let playheadClearance = 2 * Double(image.scale)

        var markers: [Marker] = []
        var run: (start: Int, end: Int, red: Bool)?
        func closeRun(_ finished: (start: Int, end: Int, red: Bool)? = nil) {
            guard let current = finished ?? run else { return }
            run = nil
            let widthInPoints = Double(current.end - current.start + 1) / Double(image.scale)
            let maxWidth = current.red ? 9.0 : 4.0
            guard widthInPoints <= maxWidth, !current.red || widthInPoints >= 2 else { return }
            let isCut = current.start == lines.lowerBound || current.end == lines.upperBound - 1
            guard !isCut || widthInPoints >= clippedMinimum else { return }
            let center = Double(current.start + current.end + 1) / 2
            markers.append(Marker(x: isVertical ? image.pointY(center) : image.pointX(center), isDownbeat: current.red))
        }

        for line in lines {
            var (reds, greys) = count(line, across: near, isVertical: isVertical, in: image)
            if isVertical {
                let other = count(line, across: far, isVertical: true, in: image)
                reds = reds > 0 && other.reds > 0 ? reds + other.reds : 0
                greys = greys > 0 && other.greys > 0 ? greys + other.greys : 0
            }
            let isRed = reds >= 2
            let isTick = !isRed && greys >= 2 && abs(Double(line) + 0.5 - playheadLine) > playheadClearance
            if isRed || isTick {
                if let current = run, line - current.end <= 2 {
                    run = (current.start, line, current.red || isRed)
                } else {
                    closeRun()
                    run = (line, line, isRed)
                }
            } else if let current = run, line - current.end > 2 {
                closeRun(current)
            }
        }
        closeRun()
        return markers
    }

    @inline(__always)
    private static func count(_ line: Int, across band: Range<Int>, isVertical: Bool, in image: PixelImage) -> (reds: Int, greys: Int) {
        var reds = 0, greys = 0
        for position in band {
            let color = isVertical ? image.pixel(position, line) : image.pixel(line, position)
            if color.isMarkerRed { reds += 1 } else if color.isTickGrey { greys += 1 }
        }
        return (reds, greys)
    }

    struct Grid {
        let reference: Double
        let spacing: Double

        func beats(from x: Double) -> Double { (x - reference) / spacing }

        func fits(_ x: Double) -> Bool {
            let k = beats(from: x)
            return abs(k - k.rounded()) < 0.2
        }

        func phase(at x: Double, downbeats: [Double]) -> Double? {
            guard !downbeats.isEmpty else { return nil }
            var votes = [0, 0, 0, 0]
            for downbeat in downbeats {
                votes[positiveModulo(Int(beats(from: downbeat).rounded()), 4)] += 1
            }
            let residue = Double(votes.indices.max { votes[$0] < votes[$1] }!)
            return positiveModulo(beats(from: x) - residue, BeatGridDetector.beatsPerBar)
        }
    }

    static func fitGrid(_ markers: [Marker], near playheadX: Double) -> Grid? {
        let xs = markers.map(\.x).sorted()
        let gaps = zip(xs.dropFirst(), xs).map { $0 - $1 }.sorted()
        let median = gaps[gaps.count / 2]
        let similar = gaps.filter { abs($0 / median - 1) < 0.15 }
        guard median > 1, !similar.isEmpty else { return nil }
        let estimate = similar.reduce(0, +) / Double(similar.count)
        func support(_ candidate: Double) -> Int {
            xs.filter { x in
                let k = (x - candidate) / estimate
                return abs(k - k.rounded()) < 0.2
            }.count
        }
        let reference = xs.map { (x: $0, support: support($0)) }.max { a, b in
            a.support != b.support ? a.support < b.support : abs(a.x - playheadX) > abs(b.x - playheadX)
        }!.x

        var count = 0.0, sumK = 0.0, sumX = 0.0, sumKK = 0.0, sumKX = 0.0
        for x in xs {
            let position = (x - reference) / estimate
            let k = position.rounded()
            guard abs(position - k) < 0.2 else { continue }
            count += 1
            sumK += k
            sumX += x
            sumKK += k * k
            sumKX += k * x
        }
        let denominator = count * sumKK - sumK * sumK
        guard count >= 2, denominator > 0 else { return nil }
        let spacing = (count * sumKX - sumK * sumX) / denominator
        return Grid(reference: (sumX - spacing * sumK) / count, spacing: spacing)
    }
}

func positiveModulo(_ value: Int, _ modulus: Int) -> Int { ((value % modulus) + modulus) % modulus }

func positiveModulo(_ value: Double, _ modulus: Double) -> Double {
    let result = value.truncatingRemainder(dividingBy: modulus)
    return result < 0 ? result + modulus : result
}

public enum PlayheadDetector {
    static let searchDistance: CGFloat = 3
    static let coverage = 0.8

    public static func locate(_ strip: WaveformStrip, in image: PixelImage) -> CGFloat? {
        let isVertical = strip.orientation == .vertical
        let area = strip.markerArea
        let candidates: Range<Int>
        let across: Range<Int>
        if isVertical {
            candidates = image.row(strip.center - searchDistance) ..< image.row(strip.center + searchDistance)
            across = image.column(area.minX) ..< image.column(area.maxX)
        } else {
            candidates = image.column(strip.center - searchDistance) ..< image.column(strip.center + searchDistance)
            across = image.row(area.minY) ..< image.row(area.maxY)
        }
        guard !candidates.isEmpty, across.count >= 8 else { return nil }
        let samples = stride(from: across.lowerBound, to: across.upperBound, by: 2)
        let total = (across.count + 1) / 2
        let allowedMisses = total - Int(Double(total) * coverage)
        var first: Int?
        var last = 0
        for line in candidates {
            var misses = 0
            for position in samples where !(isVertical ? image.pixel(position, line) : image.pixel(line, position)).isPlayheadGrey {
                misses += 1
                if misses > allowedMisses { break }
            }
            guard misses <= allowedMisses else {
                if first != nil { break }
                continue
            }
            if first == nil { first = line }
            last = line
        }
        guard let first else { return nil }
        let center = Double(first + last + 1) / 2
        return CGFloat(isVertical ? image.pointY(center) : image.pointX(center))
    }
}

public enum SyncMasterDetector {
    public static func isMaster(button: CGRect, in image: PixelImage) -> Bool {
        let left = image.column(button.minX + 4)
        let right = image.column(button.maxX - 4)
        guard right > left else { return false }
        let rows = Set([button.minY - 1, button.minY, button.minY + 1, button.maxY - 2, button.maxY - 1, button.maxY].map { image.row($0) })
        let best = rows.filter { $0 < image.height }.map { row in
            (left ..< right).filter { image.pixel($0, row).isMasterBlue }.count
        }.max() ?? 0
        return Double(best) / Double(right - left) >= 0.5
    }
}
