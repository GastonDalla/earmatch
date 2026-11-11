import CoreGraphics
import Foundation

public struct CueMarker: Equatable, Sendable {
    public enum Kind: Hashable, Sendable {
        case hotCue
        case cuePoint
    }

    public let x: Double
    public let kind: Kind
}

public enum CueCountdown {
    static let flagWidth: ClosedRange<Double> = 6 ... 16
    static let triangleWidth: ClosedRange<Double> = 6 ... 22
    static let reachedDistance = 1.0

    public static func markers(strip: CGRect, from minX: CGFloat, in image: PixelImage) -> [CueMarker] {
        let left = image.column(max(strip.minX, minX - 8))
        let right = image.column(strip.maxX)
        guard right > left else { return [] }
        var found: [CueMarker] = []

        let flagTop = image.row(strip.minY + 0.5), flagBottom = image.row(strip.minY + 3)
        let flagBody = image.row(strip.minY + 9.5)
        for run in runs(left ..< right, rows: flagTop ..< flagBottom, in: image, where: \.isHotCuePink)
        where flagWidth.contains(run.width(image)) && fill(run, row: flagBody, in: image, where: \.isHotCuePink) >= 0.5 {
            found.append(CueMarker(x: run.center(image), kind: .hotCue))
        }

        let baseTop = image.row(strip.maxY - 4), baseBottom = image.row(strip.maxY - 0.5)
        let narrow = image.row(strip.maxY - 9)
        for run in runs(left ..< right, rows: baseTop ..< baseBottom, in: image, where: \.isCueOrange)
        where triangleWidth.contains(run.width(image)) {
            let higher = fill(run, row: narrow, in: image, where: \.isCueOrange)
            if higher > 0, higher <= 0.6 { found.append(CueMarker(x: run.center(image), kind: .cuePoint)) }
        }
        return found
            .filter { $0.x > Double(minX) }
            .sorted { $0.x != $1.x ? $0.x < $1.x : $0.kind == .hotCue && $1.kind == .cuePoint }
    }

    public static func beatsToNextCue(strip: CGRect, playheadX: CGFloat, beatSpacing: Double, in image: PixelImage) -> Double? {
        guard beatSpacing > 1 else { return nil }
        let ahead = markers(strip: strip, from: playheadX + CGFloat(reachedDistance), in: image)
        return ahead.first.map { ($0.x - Double(playheadX)) / beatSpacing }
    }

    public static func counter(beatsAhead: Double?) -> BarPosition? {
        guard let beatsAhead, beatsAhead > 0 else { return nil }
        let beats = Int((beatsAhead - 1e-9).rounded(.up))
        guard beats >= 1 else { return nil }
        return BarPosition(bar: (beats - 1) / 4, beat: (beats - 1) % 4 + 1)
    }

    public static func beatsAhead(_ measured: Double, measuredAt: Double, beatsPerSecond: Double, at time: Double) -> Double {
        measured - beatsPerSecond * (time - measuredAt)
    }

    struct Run {
        var first: Int
        var last: Int

        func width(_ image: PixelImage) -> Double { Double(last - first + 1) / Double(image.scale) }
        func center(_ image: PixelImage) -> Double { image.pointX(Double(first + last + 1) / 2) }
    }

    static func runs(_ columns: Range<Int>, rows: Range<Int>, in image: PixelImage, where matches: (PixelImage.RGB) -> Bool) -> [Run] {
        guard !rows.isEmpty else { return [] }
        var result: [Run] = []
        var current: Run?
        for x in columns {
            var count = 0
            for y in rows where matches(image.pixel(x, y)) {
                count += 1
                if count >= 2 { break }
            }
            if count >= 2 {
                if var run = current, x - run.last <= 1 {
                    run.last = x
                    current = run
                } else {
                    if let run = current { result.append(run) }
                    current = Run(first: x, last: x)
                }
            }
        }
        if let run = current { result.append(run) }
        return result
    }

    static func fill(_ run: Run, row: Int, in image: PixelImage, where matches: (PixelImage.RGB) -> Bool) -> Double {
        guard row >= 0, row < image.height else { return 0 }
        var matching = 0
        for x in run.first ... run.last where matches(image.pixel(x, row)) { matching += 1 }
        return Double(matching) / Double(run.last - run.first + 1)
    }
}

extension PixelImage.RGB {
    var isHotCuePink: Bool {
        r >= 200 && (25 ... 120).contains(g) && (85 ... 165).contains(b) && Int(r) - Int(b) >= 80
    }

    var isCueOrange: Bool {
        r >= 200 && (130 ... 200).contains(g) && b <= 110 && Int(r) - Int(g) >= 35
    }
}
