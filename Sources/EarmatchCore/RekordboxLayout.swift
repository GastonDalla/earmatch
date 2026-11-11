import CoreGraphics
import Foundation

public struct AXElementSnapshot: Codable, Equatable, Sendable {
    public var role: String
    public var title: String?
    public var value: String?
    public var help: String?
    public var frame: CGRect

    public init(role: String, title: String? = nil, value: String? = nil, help: String? = nil, frame: CGRect) {
        self.role = role
        self.title = title
        self.value = value
        self.help = help
        self.frame = frame
    }
}

public struct RekordboxWindowSnapshot: Codable, Equatable, Sendable {
    public var windowFrame: CGRect
    public var elements: [AXElementSnapshot]

    public init(windowFrame: CGRect, elements: [AXElementSnapshot]) {
        self.windowFrame = windowFrame
        self.elements = elements
    }
}

public enum RekordboxMode: String, Sendable {
    case performance, export, edit, unknown
}

public enum StripOrientation: Sendable {
    case horizontal
    case vertical
}

public enum DeckArrangement: String, CaseIterable, Sendable {
    case twoDeckHorizontal = "2Deck Horizontal"
    case twoDeckVertical = "2Deck Vertical"
    case fourDeckHorizontal = "4Deck Horizontal"
    case fourDeckVertical = "4Deck Vertical"

    public var deckCount: Int {
        switch self {
        case .twoDeckHorizontal, .twoDeckVertical: 2
        case .fourDeckHorizontal, .fourDeckVertical: 4
        }
    }

    public var orientation: StripOrientation {
        switch self {
        case .twoDeckHorizontal, .fourDeckHorizontal: .horizontal
        case .twoDeckVertical, .fourDeckVertical: .vertical
        }
    }

    public var stripDecks: [Int] {
        self == .fourDeckVertical ? [3, 1, 2, 4] : Array(1 ... deckCount)
    }
}

public struct WaveformStrip: Equatable, Sendable {
    public let frame: CGRect
    public let orientation: StripOrientation
    public let markerArea: CGRect

    public init(frame: CGRect, orientation: StripOrientation, markerArea: CGRect? = nil) {
        self.frame = frame
        self.orientation = orientation
        self.markerArea = markerArea ?? frame
    }

    public var center: CGFloat { orientation == .horizontal ? frame.midX : frame.midY }
}

public struct DeckLayout: Equatable, Sendable {
    public let number: Int
    public let title: String?
    public var bpm: Double?
    public let headerBPMFrame: CGRect?
    public let jogBPMFrame: CGRect?
    public let pitchFrame: CGRect?
    public let masterButtonFrame: CGRect
    public let strip: WaveformStrip?
    public let overviewFrame: CGRect?
    public var barsText: String?
    public var elapsedSeconds: Double?
    public var remainingSeconds: Double?

    public var waveformStrip: CGRect? { strip?.frame }
}

public struct RekordboxLayout: Equatable, Sendable {
    public let mode: RekordboxMode
    public let layoutName: String?
    public let stackedWaveform: CGRect?
    public var decks: [DeckLayout]

    public var arrangement: DeckArrangement? { layoutName.flatMap(DeckArrangement.init(rawValue:)) }

    public var isSupportedPerformanceLayout: Bool {
        guard mode == .performance, let arrangement else { return false }
        return stackedWaveform != nil && decks.count == arrangement.deckCount && decks.allSatisfy { $0.strip != nil }
    }

    public static func locate(in snapshot: RekordboxWindowSnapshot) -> RekordboxLayout {
        let elements = snapshot.elements
        let modePopup = elements.first { $0.role == "AXPopUpButton" && $0.help?.hasPrefix("Change EXPORT/PERFORMANCE/EDIT mode") == true }
        let mode = modePopup.map { RekordboxMode(rawValue: ($0.value ?? "").lowercased()) ?? .unknown } ?? .unknown
        let layoutName = modePopup.flatMap { layoutPopup(nextTo: $0, in: elements)?.value }
        let waveform = layoutName.flatMap(DeckArrangement.init(rawValue:)).flatMap { mainWaveform(in: elements, arrangement: $0) }
        var decks = locateDecks(in: elements, windowWidth: snapshot.windowFrame.width, strips: waveform?.strips ?? [:])
        for index in decks.indices where decks[index].strip?.orientation == .vertical {
            decks[index].barsText = decks[index].strip.flatMap { barsText(under: $0.frame, in: elements) }
        }
        return RekordboxLayout(mode: mode, layoutName: layoutName, stackedWaveform: waveform?.stacked, decks: decks)
    }

    private static func layoutPopup(nextTo mode: AXElementSnapshot, in elements: [AXElementSnapshot]) -> AXElementSnapshot? {
        elements
            .filter { $0.role == "AXPopUpButton" && abs($0.frame.midY - mode.frame.midY) <= 8 && $0.frame.minX >= mode.frame.maxX }
            .min { $0.frame.minX < $1.frame.minX }
    }

    private static func mainWaveform(in elements: [AXElementSnapshot], arrangement: DeckArrangement) -> (stacked: CGRect, strips: [Int: WaveformStrip])? {
        let anchor = elements.first { $0.role == "AXButton" && $0.help?.hasPrefix("Capture") == true }
            ?? elements.first { $0.role == "AXButton" && $0.help?.hasPrefix("Matching") == true }
        guard let anchor else { return nil }
        let center = CGPoint(x: anchor.frame.midX, y: anchor.frame.midY)
        let groups = elements.filter { $0.role == "AXGroup" }.map(\.frame)
        guard let holder = groups.filter({ $0.contains(center) }).min(by: { $0.width * $0.height < $1.width * $1.height }) else { return nil }
        let count = arrangement.deckCount
        let share = { (part: CGFloat, whole: CGFloat) in part / whole > 0.75 / CGFloat(count) && part / whole < 1.3 / CGFloat(count) }
        let frames: [CGRect]
        let stacked: CGRect
        switch arrangement.orientation {
        case .horizontal:
            guard holder.width < 80,
                  let found = groups.filter({ abs($0.minY - holder.minY) <= 1 && abs($0.height - holder.height) <= 1 }).max(by: { $0.width < $1.width })
            else { return nil }
            stacked = found
            frames = distinct(groups.filter {
                $0.minY >= stacked.minY - 1 && $0.maxY <= stacked.maxY + 1 && $0.width >= stacked.width - 30 && share($0.height, stacked.height)
            }, by: \.minY)
        case .vertical:
            let columns = { (candidate: CGRect) in
                distinct(groups.filter {
                    abs($0.minY - candidate.minY) <= 1 && abs($0.height - candidate.height) <= 1
                        && $0.minX >= candidate.minX - 1 && $0.maxX <= candidate.maxX + 1 && share($0.width, candidate.width)
                }, by: \.minX)
            }
            guard let found = groups.first(where: {
                abs($0.minX - holder.minX) <= 1 && abs($0.width - holder.width) <= 1 && $0.height > holder.height && columns($0).count == count
            }) else { return nil }
            stacked = found
            frames = columns(found)
        }
        guard frames.count == count else { return nil }
        var strips: [Int: WaveformStrip] = [:]
        for (deck, frame) in zip(arrangement.stripDecks, frames) {
            let markerArea: CGRect? = arrangement.orientation == .vertical && holder.maxY > frame.minY
                ? CGRect(x: frame.minX, y: holder.maxY, width: frame.width, height: frame.maxY - holder.maxY)
                : nil
            strips[deck] = WaveformStrip(frame: frame, orientation: arrangement.orientation, markerArea: markerArea)
        }
        return (stacked, strips)
    }

    private static func distinct(_ frames: [CGRect], by key: KeyPath<CGRect, CGFloat>) -> [CGRect] {
        var result: [CGRect] = []
        for frame in frames where !result.contains(where: { abs($0[keyPath: key] - frame[keyPath: key]) <= 1 }) {
            result.append(frame)
        }
        return result.sorted { $0[keyPath: key] < $1[keyPath: key] }
    }

    private static func barsText(under strip: CGRect, in elements: [AXElementSnapshot]) -> String? {
        let text = elements.first {
            $0.role == "AXStaticText" && abs($0.frame.minY - strip.maxY) <= 6 && $0.frame.midX >= strip.minX && $0.frame.midX < strip.maxX
        }
        let value = text?.value?.trimmingCharacters(in: .whitespaces) ?? ""
        return value.isEmpty ? nil : value
    }

    private static func locateDecks(in elements: [AXElementSnapshot], windowWidth: CGFloat, strips: [Int: WaveformStrip]) -> [DeckLayout] {
        let masters = elements
            .filter { $0.role == "AXButton" && $0.title == "MASTER" }
            .sorted { $0.frame.minY < $1.frame.minY }
        guard masters.count == 2 || masters.count == 4 else { return [] }
        let rows = stride(from: 0, to: masters.count, by: 2).map { masters[$0 ..< $0 + 2].sorted { $0.frame.minX < $1.frame.minX } }
        guard rows.allSatisfy({ abs($0[0].frame.minY - $0[1].frame.minY) <= 6 }) else { return [] }
        let halfWidth = windowWidth / 2
        var decks: [DeckLayout] = []
        for (rowIndex, row) in rows.enumerated() {
            let top = rowIndex == 0 ? -CGFloat.infinity : row[0].frame.minY - headerHeight
            let bottom = rowIndex + 1 < rows.count ? rows[rowIndex + 1][0].frame.minY - headerHeight : .infinity
            for (column, master) in row.enumerated() {
                let number = rowIndex * 2 + column + 1
                let left = CGFloat(column) * halfWidth
                let inDeck = elements.filter {
                    (left ..< left + halfWidth).contains($0.frame.midX) && (top ..< bottom).contains($0.frame.midY)
                }
                decks.append(deck(number: number, master: master, elements: inDeck, strip: strips[number]))
            }
        }
        return decks
    }

    private static let headerHeight: CGFloat = 40

    private static func deck(number: Int, master: AXElementSnapshot, elements: [AXElementSnapshot], strip: WaveformStrip?) -> DeckLayout {
        let texts = elements.filter { $0.role == "AXStaticText" }
        let headerRow = texts.filter { abs($0.frame.minY - master.frame.minY) <= 6 && $0.frame.height <= 18 }
        let header = headerRow.first { $0.help == nil && ($0.value ?? "").range(of: #"^\d{2,3}\.\d{2}$"#, options: .regularExpression) != nil }
        let elapsed = time(matching: #"^\d{2}:\d{2}$"#, in: headerRow)
        let remaining = time(matching: #"^-\d{2}:\d{2}$"#, in: headerRow)
        let isLoaded = header != nil || (elapsed ?? 0) + (remaining ?? 0) > 0
        let title = !isLoaded ? nil : texts
            .filter { $0.frame.maxY <= master.frame.minY + 1 && $0.frame.minY >= master.frame.minY - headerHeight && !($0.value ?? "").isEmpty }
            .max { $0.frame.width < $1.frame.width }?
            .value
        let jog = elements.first { ($0.role == "AXTextArea" || $0.role == "AXStaticText") && $0.help?.hasPrefix("Deck BPM display") == true }
        return DeckLayout(
            number: number,
            title: title,
            bpm: header.flatMap { BPMDisplayFormatter.parse($0.value ?? "") },
            headerBPMFrame: header?.frame,
            jogBPMFrame: jog?.frame,
            pitchFrame: jog.flatMap { pitchFrame(under: $0.frame, texts: texts) },
            masterButtonFrame: master.frame,
            strip: strip,
            overviewFrame: overview(below: master.frame, in: elements),
            barsText: nil,
            elapsedSeconds: elapsed,
            remainingSeconds: remaining
        )
    }

    private static func overview(below master: CGRect, in elements: [AXElementSnapshot]) -> CGRect? {
        elements.first {
            $0.role == "AXGroup" && $0.frame.width >= 300 && (20 ... 50).contains($0.frame.height)
                && $0.frame.minY >= master.maxY && $0.frame.minY <= master.maxY + 30
        }?.frame
    }

    private static func pitchFrame(under jog: CGRect, texts: [AXElementSnapshot]) -> CGRect? {
        let near = texts.filter {
            abs($0.frame.minY - jog.maxY) <= 3 && $0.frame.height <= 16
                && $0.frame.minX >= jog.minX - 20 && $0.frame.minX <= jog.maxX + 20
        }
        let range = near.first { $0.help?.hasPrefix("Tempo Range") == true } ?? near.first { ($0.value ?? "").hasPrefix("±") }
        guard let rowTop = (range ?? near.first)?.frame.minY else { return nil }
        let row = near.filter { abs($0.frame.minY - rowTop) <= 1 }
        let pitch = row.filter { $0 != range && $0.frame.maxX <= (range?.frame.minX ?? .infinity) + 1 }
        guard let first = pitch.first else { return nil }
        return pitch.dropFirst().reduce(first.frame) { $0.union($1.frame) }
    }

    private static func time(matching pattern: String, in row: [AXElementSnapshot]) -> Double? {
        guard let main = row.first(where: { ($0.value ?? "").range(of: pattern, options: .regularExpression) != nil }),
              let digits = main.value?.filter(\.isNumber), digits.count == 4,
              let minutes = Int(digits.prefix(2)), let seconds = Int(digits.suffix(2))
        else { return nil }
        let tenthsText = row.first { abs($0.frame.minX - main.frame.maxX) <= 2 && ($0.value ?? "").range(of: #"^\.\d$"#, options: .regularExpression) != nil }
        let tenths = tenthsText.flatMap { Int($0.value!.dropFirst()) } ?? 0
        return Double(minutes * 600 + seconds * 10 + tenths) / 10
    }
}

extension RekordboxLayout {
    public func headerBPMElementIndices(in snapshot: RekordboxWindowSnapshot) -> [Int: Int] {
        var indices: [Int: Int] = [:]
        for deck in decks {
            guard let frame = deck.headerBPMFrame,
                  let index = snapshot.elements.firstIndex(where: { $0.role == "AXStaticText" && $0.frame == frame })
            else { continue }
            indices[deck.number] = index
        }
        return indices
    }

    public mutating func applyLiveHeaderBPM(_ texts: [Int: String]) {
        for index in decks.indices {
            guard let text = texts[decks[index].number] else { continue }
            decks[index].bpm = BPMDisplayFormatter.parse(text)
        }
    }
}
