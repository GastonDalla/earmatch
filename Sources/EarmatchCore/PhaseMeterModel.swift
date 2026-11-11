import Foundation

public struct PhaseMeterDeckInput: Equatable, Sendable {
    public var number: Int
    public var phaseInBar: Double?
    public var counter: BarPosition?

    public init(number: Int, phaseInBar: Double?, counter: BarPosition?) {
        self.number = number
        self.phaseInBar = phaseInBar
        self.counter = counter
    }
}

public enum PhaseMeterBox: Equatable, Sendable {
    case idle
    case masterBeat
    case inPhase
    case outOfPhase
}

public struct PhaseMeterTick: Equatable, Sendable {
    public let offset: Double
    public let position: Double
    public let isDownbeat: Bool
}

public struct PhaseMeterRow: Equatable, Sendable {
    public enum Role: Equatable, Sendable {
        case master
        case deck
    }

    public enum TickDirection: Equatable, Sendable {
        case up
        case down
    }

    public let role: Role
    public let deckNumber: Int
    public let isVacant: Bool
    public let beatInBar: Int?
    public let fraction: Double?
    public let isOnMastersBeat: Bool?
    public let boxes: [PhaseMeterBox]
    public let ticks: [PhaseMeterTick]
    public let tickDirection: TickDirection
    public let counter: String
}

public struct PhaseMeterLabel: Equatable, Sendable {
    public let lines: [String]
    public let player: Int
}

public struct PhaseMeterDisplay: Equatable, Sendable {
    public enum CounterSide: Equatable, Sendable {
        case left
        case right
    }

    public static let counterUnit = "Bars"
    public static let emptyCounter = "--.-"

    public let style: PhaseMeterType
    public let masterPlayer: Int?
    public let label: PhaseMeterLabel?
    public let counterSide: CounterSide
    public let span: Double
    public let rows: [PhaseMeterRow]

    public var showsMasterWaveform: Bool { style == .cdj3000Waveform && masterPlayer != nil }
}

public enum PhaseMeterModel {
    public static let lineSpan = 4.0
    public static let cdj3000Span = 4.8
    public static let masterLabelLines = ["MASTER", "PLAYER"]

    public static func span(for style: PhaseMeterType) -> Double {
        switch style {
        case .box: 0
        case .line: lineSpan
        case .cdj3000PhaseMeter, .cdj3000Waveform: cdj3000Span
        }
    }

    public static func display(for deckNumber: Int, decks: [PhaseMeterDeckInput], master: Int?, style: PhaseMeterType) -> PhaseMeterDisplay {
        let own = decks.first { $0.number == deckNumber } ?? PhaseMeterDeckInput(number: deckNumber, phaseInBar: nil, counter: nil)
        let span = span(for: style)
        let otherMaster = master.flatMap { number in number == deckNumber ? nil : decks.first { $0.number == number } }
        let counterSide: PhaseMeterDisplay.CounterSide = style.isCDJ3000 ? .left : .right

        guard let masterDeck = otherMaster else {
            var rows = [row(own, role: .deck, against: .noMaster, style: style, span: span)]
            if style.isCDJ3000 { rows.insert(vacantMasterRow(deckNumber), at: 0) }
            return PhaseMeterDisplay(style: style, masterPlayer: nil, label: nil, counterSide: counterSide, span: span, rows: rows)
        }
        let masterBeat = beat(masterDeck.phaseInBar).map(Comparison.masterBeat) ?? .masterUnread
        let label: PhaseMeterLabel? = switch style {
        case .box, .line: PhaseMeterLabel(lines: masterLabelLines, player: masterDeck.number)
        case .cdj3000PhaseMeter: nil
        case .cdj3000Waveform: PhaseMeterLabel(lines: [], player: masterDeck.number)
        }
        return PhaseMeterDisplay(style: style, masterPlayer: masterDeck.number, label: label, counterSide: counterSide, span: span, rows: [
            row(masterDeck, role: .master, against: .noMaster, style: style, span: span),
            row(own, role: .deck, against: masterBeat, style: style, span: span),
        ])
    }

    private enum Comparison {
        case noMaster
        case masterUnread
        case masterBeat(Int)
    }

    private static func row(_ deck: PhaseMeterDeckInput, role: PhaseMeterRow.Role, against comparison: Comparison,
                            style: PhaseMeterType, span: Double) -> PhaseMeterRow {
        let current = beat(deck.phaseInBar)
        let isOnMastersBeat: Bool? = switch (role, comparison, current) {
        case let (.deck, .masterBeat(master), .some(current)): master == current
        default: nil
        }
        let boxes = style != .box ? [] : (0 ..< Int(BeatGridDetector.beatsPerBar)).map { index -> PhaseMeterBox in
            guard index == current else { return .idle }
            if role == .master { return .masterBeat }
            switch comparison {
            case .noMaster: return .inPhase
            case .masterUnread: return .idle
            case .masterBeat: return isOnMastersBeat == true ? .inPhase : .outOfPhase
            }
        }
        return PhaseMeterRow(
            role: role,
            deckNumber: deck.number,
            isVacant: false,
            beatInBar: current,
            fraction: deck.phaseInBar.map { $0 - $0.rounded(.down) },
            isOnMastersBeat: isOnMastersBeat,
            boxes: boxes,
            ticks: span > 0 ? deck.phaseInBar.map { ticks(phase: $0, span: span) } ?? [] : [],
            tickDirection: role == .master ? .down : .up,
            counter: deck.counter?.label ?? PhaseMeterDisplay.emptyCounter
        )
    }

    private static func vacantMasterRow(_ deckNumber: Int) -> PhaseMeterRow {
        PhaseMeterRow(role: .master, deckNumber: deckNumber, isVacant: true, beatInBar: nil, fraction: nil, isOnMastersBeat: nil,
                      boxes: [], ticks: [], tickDirection: .down, counter: PhaseMeterDisplay.emptyCounter)
    }

    private static func beat(_ phase: Double?) -> Int? {
        phase.map { min(Int($0.rounded(.down)), Int(BeatGridDetector.beatsPerBar) - 1) }
    }

    private static func ticks(phase: Double, span: Double) -> [PhaseMeterTick] {
        let first = Int((phase - span).rounded(.up))
        let last = Int((phase + span).rounded(.down))
        guard first <= last else { return [] }
        return (first ... last).map { n in
            let offset = Double(n) - phase
            return PhaseMeterTick(offset: offset, position: offset / span,
                                  isDownbeat: positiveModulo(n, Int(BeatGridDetector.beatsPerBar)) == 0)
        }
    }
}
