import SwiftUI
import EarmatchCore

struct PhaseMeterCanvas: View {
    let display: PhaseMeterDisplay

    var body: some View {
        Canvas { context, size in
            PhaseMeterDrawing(display: display).draw(in: &context, size: size)
        }
        .accessibilityElement()
        .accessibilityLabel(PhaseMeterDrawing.accessibilityText(display))
    }
}

private struct PhaseMeterDrawing {
    let display: PhaseMeterDisplay

    private static let rowPitch: CGFloat = 18
    private static let sideColumn: CGFloat = 76
    private static let labelColumn: CGFloat = 64
    private static let badgeColumn: CGFloat = 26

    private var style: PhaseMeterType { display.style }

    func draw(in context: inout GraphicsContext, size: CGSize) {
        let rows = display.rows
        guard !rows.isEmpty else { return }
        let midY = size.height / 2
        let firstY = midY - CGFloat(rows.count - 1) * Self.rowPitch / 2
        let meter: CGRect
        switch display.counterSide {
        case .right:
            meter = CGRect(x: Self.labelColumn + 6, y: 0,
                           width: max(48, size.width - Self.labelColumn - 6 - Self.sideColumn - 10), height: size.height)
        case .left:
            meter = CGRect(x: Self.sideColumn + 10, y: 0, width: max(48, size.width - Self.sideColumn - 10 - Self.badgeColumn), height: size.height)
        }

        if style == .cdj3000Waveform {
            drawMasterBand(in: &context, meter: meter, firstY: firstY)
        }
        drawLabel(in: &context, meter: meter, midY: midY, firstY: firstY)

        for (index, row) in rows.enumerated() {
            let y = firstY + CGFloat(index) * Self.rowPitch
            context.drawLayer { layer in
                if row.beatInBar == nil && !row.isVacant { layer.opacity = 0.35 }
                switch style {
                case .box: drawBoxes(row, in: &layer, meter: meter, y: y)
                case .line, .cdj3000PhaseMeter: drawRuler(row, in: &layer, meter: meter, y: y)
                case .cdj3000Waveform: if row.role == .deck { drawRuler(row, in: &layer, meter: meter, y: y) }
                }
                drawCounter(row, in: &layer, meter: meter, y: y)
            }
        }

        if style != .box {
            let lastY = firstY + CGFloat(rows.count - 1) * Self.rowPitch
            let top = style == .cdj3000Waveform ? firstY - 9 : firstY - 10
            context.fill(Path(CGRect(x: meter.midX - 1, y: top, width: 2, height: lastY - top + 10)), with: .color(.white))
        }
    }

    private func color(_ row: PhaseMeterRow) -> Color {
        switch (row.role, style.isCDJ3000) {
        case (.master, false): WindowTheme.master
        case (.deck, false): WindowTheme.deck
        case (.master, true): WindowTheme.cdjOrange
        case (.deck, true): WindowTheme.cdjBlue
        }
    }

    private func drawLabel(in context: inout GraphicsContext, meter: CGRect, midY: CGFloat, firstY: CGFloat) {
        guard let label = display.label else { return }
        let size: CGFloat = 15
        let isRight = display.counterSide == .left
        let badge = isRight
            ? CGRect(x: meter.maxX + 6, y: firstY - size / 2, width: size, height: size)
            : CGRect(x: meter.minX - 8 - size, y: midY - size / 2, width: size, height: size)
        let orange = style.isCDJ3000 ? WindowTheme.cdjOrange : WindowTheme.master
        context.fill(Path(roundedRect: badge, cornerRadius: 2), with: .color(orange))
        context.draw(Text(verbatim: "\(label.player)").font(.system(size: 11.5, weight: .heavy)).foregroundStyle(.black),
                     at: CGPoint(x: badge.midX, y: badge.midY), anchor: .center)
        let small = Font.system(size: 7.5, weight: .heavy)
        for (index, line) in label.lines.enumerated() where !isRight {
            let y = midY + (CGFloat(index) - CGFloat(label.lines.count - 1) / 2) * 9
            context.draw(Text(line).font(small).foregroundStyle(orange), at: CGPoint(x: badge.minX - 4, y: y), anchor: .trailing)
        }
    }

    private func drawCounter(_ row: PhaseMeterRow, in context: inout GraphicsContext, meter: CGRect, y: CGFloat) {
        let value = Text(row.counter).font(TypeScale.counter).foregroundStyle(color(row))
        let unit = Text(" " + PhaseMeterDisplay.counterUnit).font(.system(size: 8, weight: .semibold)).foregroundStyle(color(row))
        switch display.counterSide {
        case .right: context.draw(value + unit, at: CGPoint(x: meter.maxX + 10, y: y), anchor: .leading)
        case .left: context.draw(value + unit, at: CGPoint(x: meter.minX - 10, y: y), anchor: .trailing)
        }
    }

    private func drawBoxes(_ row: PhaseMeterRow, in context: inout GraphicsContext, meter: CGRect, y: CGFloat) {
        let gap: CGFloat = 6
        let width = (meter.width - 3 * gap) / 4
        let height: CGFloat = 11
        let dim = row.role == .master ? WindowTheme.masterDim : WindowTheme.deckDim
        for (index, box) in row.boxes.enumerated() {
            let rect = CGRect(x: meter.minX + CGFloat(index) * (width + gap), y: y - height / 2, width: width, height: height)
            if index < 3 {
                context.fill(Path(CGRect(x: rect.maxX, y: y - 0.75, width: gap, height: 1.5)), with: .color(dim))
            }
            switch box {
            case .idle:
                context.stroke(Path(rect.insetBy(dx: 0.5, dy: 0.5)), with: .color(dim), lineWidth: 1)
            case .masterBeat:
                context.stroke(Path(rect.insetBy(dx: 1, dy: 1)), with: .color(color(row)), lineWidth: 2)
            case .inPhase:
                context.fill(Path(rect), with: .color(WindowTheme.deck))
            case .outOfPhase:
                context.fill(Path(rect), with: .color(WindowTheme.downbeat))
            }
        }
    }

    private func drawRuler(_ row: PhaseMeterRow, in context: inout GraphicsContext, meter: CGRect, y: CGFloat) {
        let thickness: CGFloat = style.isCDJ3000 ? 2 : 1.5
        context.fill(Path(CGRect(x: meter.minX, y: y - thickness / 2, width: meter.width, height: thickness)), with: .color(color(row)))
        let length: CGFloat = style.isCDJ3000 ? 7 : 5
        let toward: CGFloat = row.tickDirection == .up ? -1 : 1
        var beats = Path()
        var downbeats = Path()
        for tick in row.ticks {
            let x = meter.midX + CGFloat(tick.position) * meter.width / 2
            if tick.isDownbeat {
                let near = y + toward * length * 1.15, far = y - toward * length * 0.9
                downbeats.addRect(CGRect(x: x - 1, y: min(near, far), width: 2, height: abs(near - far)))
            } else {
                beats.addRect(CGRect(x: x - 0.7, y: min(y, y + toward * length), width: 1.4, height: length))
            }
        }
        context.fill(beats, with: .color(style.isCDJ3000 ? .white : WindowTheme.tickGrey))
        context.fill(downbeats, with: .color(WindowTheme.downbeat))
    }

    private func drawMasterBand(in context: inout GraphicsContext, meter: CGRect, firstY: CGFloat) {
        let band = CGRect(x: meter.minX, y: firstY - 8, width: meter.width, height: 16)
        context.fill(Path(band), with: .color(Color(white: 0.06)))
        guard let master = display.rows.first, master.role == .master, !master.isVacant else { return }
        let pointsPerBeat = meter.width / 2 / CGFloat(display.span)
        var body = Path()
        for tick in master.ticks {
            let start = meter.midX + CGFloat(tick.position) * meter.width / 2
            for step in 0 ..< 8 {
                let x = start + CGFloat(step) * pointsPerBeat / 10
                guard x >= band.minX, x <= band.maxX else { continue }
                let height = band.height * (tick.isDownbeat ? 0.95 : 0.8) * pow(0.72, CGFloat(step))
                body.addRect(CGRect(x: x, y: band.midY - height / 2, width: 1.2, height: height))
            }
        }
        context.fill(body, with: .color(WindowTheme.cdjBlue.opacity(0.85)))
    }

    static func accessibilityText(_ display: PhaseMeterDisplay) -> String {
        display.rows.map { row in
            if row.isVacant { return MeterSpeech.noOtherMaster }
            let who = row.role == .master ? MeterSpeech.masterDeck(row.deckNumber) : MeterSpeech.deck(row.deckNumber)
            guard let beat = row.beatInBar else { return MeterSpeech.gridNotRead(who) }
            var parts = ["\(who): \(MeterSpeech.beat(beat))"]
            if row.isOnMastersBeat == false { parts.append(MeterSpeech.outOfPhaseWithMaster) }
            parts.append(MeterSpeech.counter(row.counter))
            return parts.joined(separator: ", ")
        }
        .joined(separator: ". ")
    }
}

enum MeterSpeech {
    static let noOtherMaster = OptionText.noOtherMaster
    static let outOfPhaseWithMaster = L10n.text("out of phase with the master")
    static let noTrack = L10n.text("no track loaded")
    static let syncMaster = L10n.text("Sync Master")
    private static let deckFormat = L10n.template("Deck %ld")
    private static let masterDeckFormat = L10n.template("Master deck %ld")
    private static let gridNotReadFormat = L10n.template("%@: beat grid not read")
    private static let beatFormat = L10n.template("beat %ld of 4")
    private static let counterFormat = L10n.template("%@ bars")
    private static let outOfPhaseWithDeckFormat = L10n.template("out of phase with deck %ld")

    static func deck(_ number: Int) -> String { String(format: deckFormat, number) }
    static func masterDeck(_ number: Int) -> String { String(format: masterDeckFormat, number) }
    static func gridNotRead(_ who: String) -> String { String(format: gridNotReadFormat, who) }
    static func beat(_ beat: Int) -> String { String(format: beatFormat, beat + 1) }
    static func counter(_ counter: String) -> String { String(format: counterFormat, counter) }
    static func outOfPhase(withDeck number: Int) -> String { String(format: outOfPhaseWithDeckFormat, number) }
}
